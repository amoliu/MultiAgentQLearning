using System;
using System.Collections.Generic;
using Microsoft.SolverFoundation.Services;

namespace MultiAgentQLearning
{
    public class FriendQTable
    {
        private readonly Dictionary<TableKey, double> _qValues = new Dictionary<TableKey, double>();

        private readonly double _gamma = 0.9;
        private int _t;
        private double _alphaInit = 0.2;

        private double Alpha => _alphaInit / (1 + 0.00001 * ++_t) > 0.0001 ? _alphaInit / (1 + 0.00001 * ++_t) : 0.0001;

        public void UpdateQValue(State currentState, State nextState, Action currentPlayerAction, Action opposingPlayerAction, double currentPlayerReward)
        {
            double currentQValue;
            var qValueTableKey = new TableKey(currentState, currentPlayerAction, opposingPlayerAction);

            if (!_qValues.TryGetValue(qValueTableKey, out currentQValue))
            {
                //Default Q Value is 1.0
                currentQValue = 1.0;
            }

            //Update value using friend function
            var valNextState = GetMaxQValue(nextState);

            var nextQValue = (1 - Alpha) * currentQValue + Alpha * (currentPlayerReward + _gamma * valNextState);

            _qValues[qValueTableKey] = nextQValue;
        }

        public double GetQValue(State state, Action currentPlayerAction, Action opposingPlayerAction)
        {
            double currentQValue;
            var qValueTableKey = new TableKey(state, currentPlayerAction, opposingPlayerAction);

            if (!_qValues.TryGetValue(qValueTableKey, out currentQValue))
            {
                //Default Q Value is 1.0
                currentQValue = 1.0;
            }

            return currentQValue;
        }

        private double GetMaxQValue(State state)
        {
            var maxQValue = double.MinValue;

            foreach (Action currentPlayerAction in Enum.GetValues(typeof(Action)))
            {
                foreach (Action opposingAction in Enum.GetValues(typeof(Action)))
                {
                    double currentQValue;
                    var qValueTableKey = new TableKey(state, currentPlayerAction, opposingAction);

                    if (!_qValues.TryGetValue(qValueTableKey, out currentQValue))
                    {
                        //Default Q Value is 1.0
                        currentQValue = 1.0;
                    }

                    maxQValue = maxQValue > currentQValue ? maxQValue : currentQValue;
                }
            }

            return maxQValue;
        }

        class TableKey : IEquatable<TableKey>
        {
            private State State { get; }
            private Action CurrentPlayerAction { get; }
            private Action OpposingPlayerAction { get; }

            public TableKey(State state, Action currentPlayerAction, Action opposingPlayerAction)
            {
                State = state;
                CurrentPlayerAction = currentPlayerAction;
                OpposingPlayerAction = opposingPlayerAction;
            }

            public bool Equals(TableKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(State, other.State) && CurrentPlayerAction == other.CurrentPlayerAction && OpposingPlayerAction == other.OpposingPlayerAction;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TableKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (State != null ? State.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (int) CurrentPlayerAction;
                    hashCode = (hashCode*397) ^ (int) OpposingPlayerAction;
                    return hashCode;
                }
            }
        }

        class FoeValue
        {
            public Dictionary<Action, double> GetValue(State s, FriendQTable q)
            {
                var context = SolverContext.GetContext();
                var model = context.CreateModel();

                var actionDecisions = new List<Decision>();

                foreach (var action in Enum.GetNames(typeof(Action)))
                {
                    var decision = new Decision(Domain.RealNonnegative, action);
                    model.AddDecisions(decision);
                    actionDecisions.Add(decision);
                }

                var valueDecision = new Decision(Domain.RealNonnegative, "value");
                model.AddDecisions(valueDecision);

                model.AddConstraint("probSumConst", actionDecisions[0] + actionDecisions[1] + actionDecisions[2] + actionDecisions[3] + actionDecisions[4] == 1.0);

                int constCount = 0;

                foreach (Action playerOneAction in Enum.GetValues(typeof(Action)))
                {
                    var qConstraintValues = new List<double>();

                    foreach (Action playerTwoAction in Enum.GetValues(typeof(Action)))
                    {
                        qConstraintValues.Add(q.GetQValue(s, playerOneAction, playerTwoAction));
                    }

                    model.AddConstraint("Const" + constCount, qConstraintValues[0]*actionDecisions[0] + qConstraintValues[1]*actionDecisions[1] + qConstraintValues[2]*actionDecisions[2] + qConstraintValues[3]*actionDecisions[3] + qConstraintValues[4]*actionDecisions[4] <= valueDecision);

                    ++constCount;
                }

                model.AddGoal("MinimizeV", GoalKind.Minimize, valueDecision);

                context.Solve(new SimplexDirective());

                var pi_s = new Dictionary<Action, double>();

                foreach (var actionDec in actionDecisions)
                {
                    pi_s[(Action)Enum.Parse(typeof(Action), actionDec.Name)] = actionDec.GetDouble();
                }

                context.ClearModel();

                return pi_s;
            }
        }
    }
}
