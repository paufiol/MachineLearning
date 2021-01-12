using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Agent : MonoBehaviour {

	// Hyperparameters of the reinforcement learning

	// Update rate of learned values
	float learningRate = 0.5f;	
	// Discount factor when computing the Q values
	float gamma = 0.99f;
	// Starting epsilon (choose random actions)
	float epsilon = 1.0f;
	// Minimum epsilon (after coolingSteps)
	float minEpsilon = 0.1f;
	// Factor to reduce epsilon (number of actions that it
	// takes it to go down to minEpsilon)
	int coolingSteps = 100;


	// The Q table: game states x actions
	private float [, ] qTable;
	private int states, actions;
	private int rows, cols, breakables;

	// The agent remembers the last state to apply the rewards received
	// once the action has been executed
	private int lastState;

	// Last action chosen (-1 none yet)
	int action = -1;


	void Start () {
	}

	// Gets the game information from the game to resize the state table
	public void GameSetup(int nRows, int nCols, int nBreakables) {

		// Number of states has two components:
		// a) Position of the agent on the board (rows x cols)
		// b) State of the breakables (intact/broken) -> 2^# of breakables
		// The number of states is the product of a) and b) (all possible combinations)

		rows       = nRows;
		cols       = nCols;
		breakables = nBreakables;

		states     = rows * cols * Mathf.FloorToInt(Mathf.Pow(2, breakables));

		// 4 actions in this game: right, up, left, down
		actions = 4;
		qTable  = new float[states, actions];	

		for (int i = 0; i < states; i++)
			for (int j = 0; j < actions; j++)
				qTable [i, j] = 0.0f;
	}

	// Ask the agent the next action to execute
	public int ChooseAction(int gameState) {
		lastState = gameState;

		// Decide if it is going to be a random action or
		// according to Q table
		if (Random.Range (0.0f, 1.0f) < epsilon) {
			// Random.Range with ints does not include the maximum
			action = Random.Range (0, actions);		
		} else {
			action = BestAction(lastState);
		}

		// Reduce epsilon (to gradually reduce the random aspect)
		if (epsilon > minEpsilon) { 
			epsilon -= ((1.0f - minEpsilon) / (float)coolingSteps); 
		}

		return action;
	}

	// Give the agent the reward of its last action 
	// It needs to know the new state to get some of its reward (future reward)
	public void GetReward(int newState, float reward, bool episodeEnds) {

		if (episodeEnds) {
			qTable [lastState, action] += learningRate * (reward - qTable [lastState, action]);
		} else {
			float bestRewardNewState = qTable [newState, BestAction (newState)];
			qTable [lastState, action] += learningRate * (reward + gamma * bestRewardNewState - qTable [lastState, action]); 
		}

		lastState = newState;
	}
				

	// Returns the best action of the Q table given the current state
	private int BestAction(int currentState) {
		int posMax = 0;
		for (int j = 1; j < actions; j++)
			if (qTable [currentState, j] > qTable [currentState, posMax])
				posMax = j;
		return posMax;
	}

}
