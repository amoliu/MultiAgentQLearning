using System;
using System.Collections.Generic;

namespace MultiAgentQLearning
{
    public class QLearnerQTable
    {
        private readonly Dictionary<TableKey, double> _qValues = new Dictionary<TableKey, double>();

        private readonly double _gamma = 0.9;
        private int _t;
        private double _alphaInit = 0.001;

        private double Alpha => _alphaInit/(1 + 0.00001 * ++_t) > 0.001 ? _alphaInit / (1 + 0.00001 * ++_t) : 0.001;

        public double UpdateQValue(State state, State nextState, Action currentPlayerAction, double currentPlayerReward)
        {
            double currentQValue;
            var qValueTableKey = new TableKey(state, currentPlayerAction);

            if (!_qValues.TryGetValue(qValueTableKey, out currentQValue))
            {
                //Default Q Value is 1.0
                currentQValue = 1.0;
            }

            //Update value table with current state
            var nextStateV = GetMaxQValue(nextState);

            //Q value update
            var updatedQValue = (1 - Alpha) * currentQValue + Alpha * (currentPlayerReward + _gamma * nextStateV);

            _qValues[qValueTableKey] = updatedQValue;

            return updatedQValue;
        }

        public double GetQValue(State state, Action playerAction)
        {
            double qValue;
            if (!_qValues.TryGetValue(new TableKey(state, playerAction), out qValue))
            {
                //Defaults to 1.0
                qValue = 1.0;
            }

            return qValue;
        }

        private double GetMaxQValue(State nextState)
        {
            var maxQValue = double.MinValue;

            foreach (Action action in Enum.GetValues(typeof(Action)))
            {
                double currentQValue;
                var qValueTableKey = new TableKey(nextState, action);

                if (!_qValues.TryGetValue(qValueTableKey, out currentQValue))
                {
                    //Default Q Value is 1.0
                    currentQValue = 1.0;
                }

                maxQValue = maxQValue > currentQValue ? maxQValue : currentQValue;
            }

            return maxQValue;
        }

        class TableKey : IEquatable<TableKey>
        {
            private State State { get; }
            private Action CurrentPlayerAction { get; }

            public TableKey(State state, Action currentPlayerAction)
            {
                State = state;
                CurrentPlayerAction = currentPlayerAction;
            }

            public bool Equals(TableKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(State, other.State) && CurrentPlayerAction == other.CurrentPlayerAction;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TableKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (State != null ? State.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (int)CurrentPlayerAction;
                    return hashCode;
                }
            }
        }
    }
}
