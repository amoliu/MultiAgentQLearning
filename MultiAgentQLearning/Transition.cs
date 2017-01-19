using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiAgentQLearning
{
    class Transition
    {
        private Dictionary<ProbabilityTransitionKey, List<State>> _transitionTable = new Dictionary<ProbabilityTransitionKey, List<State>>();
        private Random _random = new Random();

        public Transition(StateSet states, JointActionSet actions)
        {
            foreach (State currentState in states)
            {
                foreach (JointAction action in actions)
                {
                    AddNextStateProbabilities(currentState, action);
                }
            }
        }

        public State GetNextState(State currentState, JointAction action)
        {
            var possibleStates = _transitionTable[new ProbabilityTransitionKey(currentState, action)];
            return possibleStates[_random.Next(possibleStates.Count)];
        }

        private void AddNextStateProbabilities(State currentState, JointAction action)
        {
            var possibleNextStates = new List<State>();

            var nextPlayerAPosition = GetNextPosition(currentState.PlayerAPosition, action.CurrentPlayerAction);
            var nextPlayerBPosition = GetNextPosition(currentState.PlayerBPosition, action.OpposingPlayerAction);

            //"When a player executes an action that would take it to the square occupied by the other player, possession of
            // the ball goes to the stationary player and the move does not take place"

            // Player A goes first and runs into player B's current position
            // or Player B goes first and runs into player A's position
            // "If the sequences of actions causes the players to collide, then only the first moves"
            if (nextPlayerAPosition == currentState.PlayerBPosition && nextPlayerBPosition == currentState.PlayerAPosition)
            {
                //50% chance that the player with the ball moves first in this scenario, and nobody moves.  As such, 50% chance the possession is changed to the other player.
                possibleNextStates.Add(new State(currentState.PlayerAPosition, currentState.PlayerBPosition, BallPossessor.A));
                possibleNextStates.Add(new State(currentState.PlayerAPosition, currentState.PlayerBPosition, BallPossessor.B));
            }
            //The second move will result in a collision.  Only the first move takes place and ball changes possession only if the second player possesses the ball
            else if (nextPlayerAPosition == nextPlayerBPosition)
            {
                possibleNextStates.Add(new State(currentState.PlayerAPosition, nextPlayerBPosition, BallPossessor.B));
                possibleNextStates.Add(new State(nextPlayerAPosition, currentState.PlayerBPosition, BallPossessor.A));
            }
            else if (nextPlayerAPosition == currentState.PlayerBPosition && nextPlayerBPosition != currentState.PlayerAPosition)
            {
                if (currentState.Possessor == BallPossessor.A)
                {
                    possibleNextStates.Add(new State(currentState.PlayerAPosition, currentState.PlayerBPosition, BallPossessor.B));
                }
                else
                {
                    possibleNextStates.Add(new State(currentState.PlayerAPosition, currentState.PlayerBPosition, BallPossessor.A));
                }

                possibleNextStates.Add(new State(nextPlayerAPosition, nextPlayerBPosition, currentState.Possessor));
            }
            else if (nextPlayerAPosition != currentState.PlayerBPosition && nextPlayerBPosition == currentState.PlayerAPosition)
            {
                if (currentState.Possessor == BallPossessor.B)
                {
                    possibleNextStates.Add(new State(currentState.PlayerAPosition, currentState.PlayerBPosition, BallPossessor.A));
                }
                else
                {
                    possibleNextStates.Add(new State(currentState.PlayerAPosition, currentState.PlayerBPosition, BallPossessor.B));
                }

                possibleNextStates.Add(new State(nextPlayerAPosition, nextPlayerBPosition, currentState.Possessor));
            }
            //No collision took place, deterministically move the players to their next locations
            else
            {
                possibleNextStates.Add(new State(nextPlayerAPosition, nextPlayerBPosition, currentState.Possessor));
            }

            possibleNextStates.RemoveAll(s => s.PlayerAPosition == s.PlayerBPosition);

            _transitionTable[new ProbabilityTransitionKey(currentState, action)] = possibleNextStates;
        }

        private int GetNextPosition(int currentPosition, Action action)
        {
            var nextPosition = 0;

            //Boundaries
            if (currentPosition >= 0 && currentPosition <= 3)
            {
                if (action == Action.North
                    || currentPosition == 0 && action == Action.West
                    || currentPosition == 3 && action == Action.East)
                {
                    nextPosition = currentPosition;
                }
                else
                {
                    nextPosition = currentPosition + MapActionToGridShift(action);
                }
            }
            else if (currentPosition >= 4 && currentPosition <= 7)
            {
                if (action == Action.South
                    || currentPosition == 4 && action == Action.West
                    || currentPosition == 7 && action == Action.East)
                {
                    nextPosition = currentPosition;
                }
                else
                {
                    nextPosition = currentPosition + MapActionToGridShift(action);
                }
            }
            else
            {
                nextPosition = currentPosition + MapActionToGridShift(action);
            }

            return nextPosition;
        }

        private int MapActionToGridShift(Action action)
        {
            switch (action)
            {
                case Action.North:
                    return -4;
                case Action.South:
                    return 4;
                case Action.East:
                    return 1;
                case Action.West:
                    return -1;
                case Action.Stick:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private class ProbabilityTransitionKey : IEquatable<ProbabilityTransitionKey>
        {
            public ProbabilityTransitionKey(State currentState, JointAction action)
            {
                CurrentState = currentState;
                Action = action;
            }

            private State CurrentState { get; }
            private JointAction Action { get; }

            public bool Equals(ProbabilityTransitionKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(CurrentState, other.CurrentState) && Equals(Action, other.Action);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ProbabilityTransitionKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = CurrentState?.GetHashCode() ?? 0;
                    hashCode = (hashCode*397) ^ (Action?.GetHashCode() ?? 0);
                    return hashCode;
                }
            }
        }
    }
}
