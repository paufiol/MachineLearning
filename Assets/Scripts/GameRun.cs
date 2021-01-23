using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRun : MonoBehaviour
{

	// Management of sprites
	private Object[] backgrounds;
	private Object[] props;
	private Object[] chars;

	// Game management
	private GameObject enemyCards;
    private GameObject ownCards;
    private int [] enemyChars;
    private int [] ownChars;
    private Agent agent;
    private int[] animalSprites = {1,6,12 }; //Fox,Frog,Opossum   0 = fox           12 = opossum

    private int NUM_OWN_CARDS = 3;
    private int NUM_ENEMY_CARDS = 3;
	private int NUM_CLASSES     = 3;
	private int DECK_SIZE       = 4;

	// Rewards
	private float RWD_ACTION_INVALID = -2.0f;
	private float RWD_HAND_LOST      = -1.0f;
	private float RWD_TIE            = -0.1f;
	private float RWD_HAND_WON       =  1.0f;

    //Tracking
    int ties = 0;
    int wins = 0;
    int losses = 0;
    int invalids = 0;
    float accuracy = 0;
    public Sprite[] displayChars;
    public SpriteRenderer[] spriteRenderers;

    // Other UI elements
    private UnityEngine.UI.Text textDeck;
    private UnityEngine.UI.Text textAction;
    private UnityEngine.UI.Text textEnemyAction;
    private UnityEngine.UI.Text textTies;
    private UnityEngine.UI.Text textWins;
    private UnityEngine.UI.Text textLosses;
    private UnityEngine.UI.Text textInvalids;
    private UnityEngine.UI.Text textAccuracy;
    // Start is called before the first frame update
    void Start()
    {
        ///////////////////////////////////////
        // Sprite management
        ///////////////////////////////////////

        // Load all prefabs
        backgrounds = Resources.LoadAll("Backgrounds/");
        props       = Resources.LoadAll("Props/");
        chars       = Resources.LoadAll("Chars/");

        //int i = 0;
        //foreach (Transform child in GameObject.Find("PlayerDeck").transform)
        //{
        //    GameObject it = child.gameObject;
        //    spriteRenderers[i] = child.GetComponent<SpriteRenderer>();
        //    i++;
        //}


        ///////////////////////////////////////
        // UI management
        ///////////////////////////////////////
        textDeck = GameObject.Find("TextDeck").GetComponent<UnityEngine.UI.Text>();
        textAction = GameObject.Find("TextAction").GetComponent<UnityEngine.UI.Text>();
        textEnemyAction = GameObject.Find("TextEnemyPlay").GetComponent<UnityEngine.UI.Text>();
        textTies = GameObject.Find("TextTies").GetComponent<UnityEngine.UI.Text>();
        textWins = GameObject.Find("TextWins").GetComponent<UnityEngine.UI.Text>();
        textLosses = GameObject.Find("TextLosses").GetComponent<UnityEngine.UI.Text>();
        textInvalids = GameObject.Find("TextInvalids").GetComponent<UnityEngine.UI.Text>();
        textAccuracy = GameObject.Find("TextAccuracy").GetComponent<UnityEngine.UI.Text>();





        ///////////////////////////////////////
        // Game management
        ///////////////////////////////////////
        enemyCards = GameObject.Find("EnemyCards");
        enemyChars = new int[NUM_ENEMY_CARDS];

        ownCards = GameObject.Find("PlayerCards");
        ownChars = new int[NUM_OWN_CARDS];

        agent      = GameObject.Find("AgentManager").GetComponent<Agent>();

        agent.Initialize();


        ///////////////////////////////////////
        // Start the game
        ///////////////////////////////////////
        StartCoroutine("GenerateTurn");


        ///////////////////////////////////////
        // Image generation
        ///////////////////////////////////////
    	//renderTexture = gameObject.GetComponent<Camera>().targetTexture;

    	//imgWidth  = renderTexture.width;
    	//imgHeight = renderTexture.height;

        
    }


    // Generate a card on a given transform
    // Return the label (0-2) of the card
    private int GenerateCard(Transform parent)
    {

    	int idx = Random.Range(0, backgrounds.Length);
    	Instantiate(backgrounds[idx], parent.position, Quaternion.identity, parent);


    	idx               = Random.Range(0, props.Length);
    	Vector3 position = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), -1.0f);
   	  	Instantiate(props[idx], parent.position+position, Quaternion.identity, parent);

    	idx         = Random.Range(0, chars.Length);
    	position    = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), -2.0f);    	
   	  	Instantiate(chars[idx], parent.position+position, Quaternion.identity, parent);

   	  	// Determine label of the character, return it
   	  	int label = 0; //smork
   	  	if(chars[idx].name.StartsWith("frog")) label = 1;
   	  	else if(chars[idx].name.StartsWith("opossum")) label = 2;

    	return label;
    } 

    // Generate another turn
    IEnumerator GenerateTurn()
    {	
    	for(int turn=0; turn<100000; turn++) {

	        ///////////////////////////////////////
	        // Generate enemy cards
	        ///////////////////////////////////////

	    	// Destroy previous sprites (if any) and generate new cards
	    	int c = 0;
	    	foreach(Transform card in enemyCards.transform) {
	    		foreach(Transform sprite in card) {
	    			Destroy(sprite.gameObject);
	    		}

	    		enemyChars[c++] = GenerateCard(card);
	    	}

            ///////////////////////////////////////
            // Generate player deck
            ///////////////////////////////////////
            int [] deck   = GeneratePlayerDeck();
	        textDeck.text = "Deck: ";
            ChangeDeckSprites(deck);
	        //foreach(int card in deck)
	        //	textDeck.text += card.ToString() + "/";

            //yield end frame

            ///////////////////////////////////////
            // Tell the player to play
            ///////////////////////////////////////
            ///
            int[] action = agent.Play(deck, enemyChars);

            int b = 0;
            foreach (Transform card in ownCards.transform)
            {
                foreach (Transform sprite in card)
                {
                    Destroy(sprite.gameObject);
                }
                int idx = Random.Range(0, backgrounds.Length);
                Instantiate(backgrounds[idx], card.transform.position, Quaternion.identity, card.transform);
                Instantiate(chars[animalSprites[action[b]]], card.transform.position + new Vector3(0, 0, -1), Quaternion.identity, card.transform);
                b++;
            }

            textAction.text = " Action:";
            foreach (int a in action)
            {
                textAction.text += a.ToString() + "/" ;
            }
            textAction.text += "In Turn: " + turn.ToString();

            textEnemyAction.text = "Enemy Action: ";
            foreach (int a in enemyChars)
            {
                textEnemyAction.text += a.ToString() + "/";
            }
            ///////////////////////////////////////
            // Compute reward
            ///////////////////////////////////////
            float reward = ComputeReward(deck, action);

            UpdateUI(reward,turn);
	        
	        Debug.Log("Turn/reward: " + turn.ToString() + "->" + reward.ToString());

	        agent.GetReward(reward);

            ///////////////////////////////////////
            // Manage turns/games
            ///////////////////////////////////////

            // IMPORTANT: wait until the frame is rendered so the player sees
            //            the newly generated cards (otherwise it will see the previous ones)
            yield return new WaitForEndOfFrame();

            //yield return new WaitForSeconds(0.1f);

    	}

    }


    // Auxiliary methods
    private int [] GeneratePlayerDeck()
    {
    	int [] deck = new int [DECK_SIZE];

    	for(int i=0; i<DECK_SIZE; i++)
    	{
    		deck[i] = Random.Range(0, NUM_CLASSES);  // high limit is exclusive so [0, NUM_CLASSES-1]
    	}

    	return deck;
    }

    // Compute the result of the turn and return the associated reward 
    // given the cards selected by the agent (action)
   	// deck -> array with the number of cards of each class the player has
   	// action -> array with the class of each card played
    private float ComputeReward(int [] deck, int [] action)
    {
        int[] tmpDeck = deck;
        bool invalid = true;
        // First check if the action is valid given the player's deck
        foreach (int card in action)
        {
            invalid = true;
            for (int i = 0; i < tmpDeck.Length;i++)
            {
                if(tmpDeck[i] == card)
                {
                    tmpDeck[i] = -1;
                    invalid = false;
                    break;
                }
            }

            if (invalid)
            {
                return RWD_ACTION_INVALID;
            }
        }

        // Second see who wins
        int score = 0;
    	for(int i=0; i < NUM_ENEMY_CARDS; i++)
    	{
    		if(action[i] != enemyChars[i])
    		{
    			if(action[i] > enemyChars[i] || action[i]==0 && enemyChars[i]==2)	
    				score++;
    			else
    				score--;
    		}
    		
    	}

    	if(score == 0) return RWD_TIE;
    	else if(score > 0) return RWD_HAND_WON;
    	else return RWD_HAND_LOST;
    }

    void UpdateUI(float reward,int turn)
    {
        switch (reward)
        {
            case -2.0f:
                invalids++;
                textInvalids.text = "Invalids: " + invalids.ToString();
                break;

            case -1.0f:
                losses++;
                textLosses.text = "Losses: " + losses.ToString();
                break;
            case -0.1f:
                ties++;
                textTies.text = "Ties: " + ties.ToString();
                break;
            case 1.0f:
                wins++;
                textWins.text = "Wins: " + wins.ToString();
                break;
        }

        if(turn != 0)
            accuracy = wins / (float)turn;

        textAccuracy.text = "Accuracy: " + accuracy.ToString();
        //Debug.Log(accuracy);
    }

    void ChangeDeckSprites(int[]deck)
    {
        for(int i = 0; i < DECK_SIZE;i++)
        {
            spriteRenderers[i].sprite = displayChars[deck[i]];
        }
    }
}
