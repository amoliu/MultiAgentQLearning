using System;
using System.Collections;
using System.Collections.Generic;

namespace MultiAgentQLearning
{
    public class JointActionSet : IEnumerable
    {
        private readonly List<JointAction> _jointActionSet = new List<JointAction>();
        private readonly Random _random = new Random();
        private readonly double _gamma = 0.9;
        private int _t;
        private double _epsilonInit = 0.5;

        private double Epsilon => 1;//_epsilonInit / (1 + 0.0001 * ++_t) > 0.001 ? _epsilonInit / (1 + 0.0001 * ++_t) : 0.001;

        public JointActionSet()
        {
            foreach (Action playerAAction in Enum.GetValues(typeof(Action)))
            {
                foreach (Action playerBAction in Enum.GetValues(typeof(Action)))
                {
                    _jointActionSet.Add(new JointAction(playerAAction, playerBAction));
                }
            }
        }

        public IEnumerator GetEnumerator()
        {
            return _jointActionSet.GetEnumerator();
        }

        public Action GetNextAction()
        {
                Array values = Enum.GetValues(typeof(Action));
                return (Action) values.GetValue(_random.Next(values.Length));
        }

        public JointAction GetNextJointAction()
        {
            Array values = Enum.GetValues(typeof(Action));
            return new JointAction((Action)values.GetValue(_random.Next(values.Length)), (Action)values.GetValue(_random.Next(values.Length)));
        }
    }

    public class JointAction : IEquatable<JointAction>
    {
        public Action CurrentPlayerAction { get; }
        public Action OpposingPlayerAction { get; }

        public JointAction(Action currentPlayerAction, Action opposingPlayerAction)
        {
            CurrentPlayerAction = currentPlayerAction;
            OpposingPlayerAction = opposingPlayerAction;
        }

        public bool Equals(JointAction other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CurrentPlayerAction == other.CurrentPlayerAction && OpposingPlayerAction == other.OpposingPlayerAction;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((JointAction) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) CurrentPlayerAction*397) ^ (int) OpposingPlayerAction;
            }
        }
    }

    public enum Action
    {
        North,
        South,
        East,
        West,
        Stick
    }
}
