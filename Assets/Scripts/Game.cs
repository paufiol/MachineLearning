using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Game: MonoBehaviour {

	public float turnDuration;
	public float timeBetweenEpisodes;
	public GameObject prefabWall;
	public GameObject prefabBreakable;



	// Rewards
	public float walkReward;
	public float breakReward;
	public float hitWallReward;
	public float hitLimitReward;
	public float goalReward;

	public int episodeTurns;

	private float timeThisTurn;

	private const int ROWS = 8;
	private const int COLS = 8;
	private GameObject[, ] board_go;
	private int[, ] board_int;
	private string breakables;			// String of 0/1 with 0=block, 1=broken
	private Agent agent;
	private int agentRow, agentCol;		// To locate the agent

	private int turn;					// Turn number in this episode
	private int episode;				// Episode number
	private float episodeReward;		// Accumulated reward in this episode
	private float globalReward;			// Accumulated reward in all episodes
	private float avgReward;			// Average reward in all episodes

	private bool goalReached;			// True if the agent reaches the exit

	private Text textEpisode, textTurn, textAvgRwd, textReward;



	void Start () {

		textEpisode = GameObject.Find ("TextEpisode")  .GetComponent<Text> ();
		textTurn    = GameObject.Find ("TextTurn")     .GetComponent<Text> ();
		textAvgRwd  = GameObject.Find ("TextAvgReward").GetComponent<Text> ();
		textReward  = GameObject.Find ("TextReward")   .GetComponent<Text> ();

		// Start the board, create all gameobjects (walls, destructibles)
		FillBoard();

		// Start the agent
		StartAgent ();

		// Start the training!
		episode      = 0;
		globalReward = 0.0f;
		avgReward    = 0.0f;
		StartEpisode();
	}

	// Initialize the matrices that hold the board information;
	// create the gameobjects of the board
	void FillBoard() {
		// 0: free; 1: breakable; 2: wall; 3: goal
		// This matrix does not change even if blocks are broken
		board_int = new int       [ROWS, COLS] {
			{0, 2, 0, 1, 0, 2, 0, 0},
			{0, 2, 0, 2, 0, 0, 1, 0},
			{0, 0, 0, 2, 2, 0, 2, 0},
			{2, 2, 1, 2, 0, 0, 0, 0},
			{0, 0, 0, 2, 1, 2, 2, 2},
			{0, 2, 2, 2, 0, 0, 0, 0},
			{0, 0, 0, 2, 0, 0, 2, 0},
			{0, 2, 0, 0, 0, 0, 2, 3}
		};


		// Instantiate the blocks (walls and breakables)
		// NOTE: rows -> movement along z; cols -> movement along x
		board_go  = new GameObject[ROWS, COLS];
		for(int i=0; i < ROWS; i++)
			for (int j = 0; j < COLS; j++)
				if (board_int[i, j] == 2) {
					Vector3 pos = new Vector3 (j, 0.0f, i);
					board_go [i, j] = Instantiate (prefabWall, pos, Quaternion.identity);
				} else if (board_int[i, j] == 1) {
					Vector3 pos = new Vector3 (j, 0.0f, i);
					board_go [i, j] = Instantiate (prefabBreakable, pos, Quaternion.identity);
				}

		// Generate a string of 0/1 with the intact/broken state of each breakable cell
		UpdateBreakables();
	}

	// Initial setup of the game agent (player)
	void StartAgent() {
		agent = GameObject.Find ("Player").GetComponent<Agent> ();

		// Tell it how many rows, cols and breakables so it can resize its state table
		agent.GameSetup(ROWS, COLS, breakables.Length);
	}

	// Starts a new game (episode)
	void StartEpisode() {

		timeThisTurn = -timeBetweenEpisodes;


		// Reactivate broken blocks
		for(int i=0; i < ROWS; i++)
			for (int j = 0; j < COLS; j++)
				if(board_go[i, j] != null && board_int[i,  j] == 1) 
					board_go[i, j].SetActive(true);

		UpdateBreakables();


		// The agent starts at position 0, 0
		agentRow = 0;
		agentCol = 0;

		// Renew the goal reached flag
		goalReached = false;

		agent.gameObject.transform.position = new Vector3 (agentRow, 0.0f, agentCol);


		// Update counters
		globalReward  += episodeReward;
		if(episode > 0) {
			avgReward = globalReward/episode;
		}

		turn          = 0;
		episodeReward = 0.0f;
		episode++;
	}


	void Update () {
		timeThisTurn += Time.deltaTime;
		if (timeThisTurn >= turnDuration) {
			timeThisTurn -= turnDuration;
			NewTurn ();
		}
	}

	// Runs a new turn in the current episode: decide action, compute rewards, and so on
	void NewTurn() {
		// 1) Ask the agent to choose an action given the current game state
		int state  = ComputeState();
		int action = agent.ChooseAction(state);


		// 2) The agent performs the action and the reward is computed
		float reward   = MoveAgent(action);
		episodeReward += reward;

		// 3) Give the agent its rewards
		turn++;

		int newState     = ComputeState();
		bool episodeEnds = goalReached || turn >= episodeTurns;

		agent.GetReward(newState, reward, episodeEnds);

		// 4) If the episode ends then start a new one
		if (episodeEnds)
			StartEpisode ();

		// 5) Update learning texts on screen
		textEpisode.text = "Episode: " + episode.ToString();
		textTurn.text    = "Turn:    " + turn.ToString();
		textAvgRwd.text  = "Avg rwd: " + avgReward.ToString();
		textReward.text  = "Reward:  " + episodeReward.ToString();
	}


	// Move agent according to the action performed (if possible), compute reward
	float MoveAgent(int action) {
		float turnReward = 0.0f;

		// 1) Determine destination cell
		int destRow = agentRow;
		int destCol = agentCol;

		switch(action) {
			case 0:      
				destCol++;  // Right  
				break;

			case 1:
				destRow++;  // Up
				break;

			case 2:
				destCol--;  // Left
				break;

			case 3:
				destRow--;  // Down
				break;
		}

		// 2) Discard out-of-limits attempts; do not move and end action
		if(destCol < 0 || destRow < 0 || destCol >= COLS || destRow >= ROWS) {
			turnReward +=  hitLimitReward;
			return turnReward;
		}


		// 3) Detect destination cell type, act accordingly
		int destCell = board_int[destRow, destCol];

		switch(destCell) {
			case 0:			// Walkable
				agentRow = destRow;
				agentCol = destCol;
				turnReward += walkReward;
				break;

			case 1: 		// Breakable: break it if needed, walk there otherwise
				if(board_go[destRow, destCol].activeSelf) {

					board_go[destRow, destCol].SetActive(false);
					UpdateBreakables();
					turnReward += breakReward;
				} else {
					turnReward += walkReward;
				}

				agentRow = destRow;
				agentCol = destCol;
				break;

			case 2: 		// Hard wall: do not move
				turnReward += hitWallReward;
				break;

			case 3: 		// Goal reached
				turnReward += goalReward;
				agentRow    = destRow;
				agentCol    = destCol;
				goalReached = true;
				break;
		}


		// Execute the movement and return the reward
		agent.gameObject.transform.position = new Vector3(agentCol, 0.0f, agentRow);
		return turnReward;
	}

	// Update the array with the state of the breakables according to the active/inactive state of the gameobjects
	private void UpdateBreakables() {

		breakables = "";

		for(int i=0; i < ROWS; i++)
			for (int j = 0; j < COLS; j++) 
				if(board_int[i, j] == 1) {
				 	breakables += board_go[i, j].activeSelf? "0" : "1";
			}
	}

	// Transforms the state of the game (blocks destroyed, player position) into an integer
	// that will be the row number of the agent's Q table
	// It does two things:
	// 1) Take the state of breakable blocks (string of 0/1) as a binary number; transform
	// it into an integer (for example "0101" -> 9)
	// 2) Transform the agent's position (row, col) into an integer
	// 3) Combine both numbers to get all possible combinations
	// The state of the game is defined by the elements that may change. There is no need
	// to include the state of the solid walls or the empty cells
	private int ComputeState() {

		UpdateBreakables();

		int stateBreakables = System.Convert.ToInt32 (breakables, 2);
		int agentPosition   = agentRow*COLS + agentCol;

		int state = breakables.Length * agentPosition + stateBreakables;
		return state;
	}

}



