using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SolverFoundation.Services;

namespace MultiAgentQLearning
{
    public class CorrelatedQTable
    {
        private readonly Dictionary<TableKey, double> _qValues = new Dictionary<TableKey, double>();
        private readonly Dictionary<TableKey, double> _opponentQValues = new Dictionary<TableKey, double>();

        private readonly double _gamma = 0.9;
        private int _t;
        public double _alpha = 0.2;

        public void UpdateQValue(State currentState, State nextState, Action currentPlayerAction, Action opposingPlayerAction, double currentPlayerReward, double opponentPlayerReward, bool gameover)
        {
            var qValueTableKey = new TableKey(currentState, currentPlayerAction, opposingPlayerAction);
            var currentQValue = GetCurrentPlayerQValue(currentState, currentPlayerAction, opposingPlayerAction);
            var opponentCurrentQValue = GetOpponentQValue(currentState, currentPlayerAction, opposingPlayerAction);

            Tuple<double, double> valNextState = new Tuple<double, double>(0.0, 0.0);
            if (!gameover)
            {
                //Update value using foe function
                valNextState = CorrelatedValue.GetValue(nextState, this);
            }

            var nextQValueCurrent = (1 - _alpha) * currentQValue + _alpha * (currentPlayerReward + _gamma * valNextState.Item1);
            var nextQValueOpponent = (1 - _alpha) * opponentCurrentQValue + _alpha * (opponentPlayerReward + _gamma * valNextState.Item2);

            _qValues[qValueTableKey] = nextQValueCurrent;
            _opponentQValues[qValueTableKey] = nextQValueOpponent;

            //Decay alpha
            ++_t;
            _alpha = _alpha / (1 + 0.0000000001 * ++_t) > 0.001 ? _alpha / (1 + 0.0000000001 * ++_t) : 0.001;
        }

        public double GetCurrentPlayerQValue(State state, Action currentPlayerAction, Action opposingPlayerAction)
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

        public double GetOpponentQValue(State state, Action currentPlayerAction, Action opposingPlayerAction)
        {
            double currentQValue;
            var qValueTableKey = new TableKey(state, currentPlayerAction, opposingPlayerAction);

            if (!_opponentQValues.TryGetValue(qValueTableKey, out currentQValue))
            {
                //Default Q Value is 1.0
                currentQValue = 1.0;
            }

            return currentQValue;
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
                    var hashCode = State?.GetHashCode() ?? 0;
                    hashCode = (hashCode*397) ^ (int) CurrentPlayerAction;
                    hashCode = (hashCode*397) ^ (int) OpposingPlayerAction;
                    return hashCode;
                }
            }
        }

        internal static class CorrelatedValue
        {
            public static Tuple<double, double> GetValue(State s, CorrelatedQTable q)
            {
                var context = SolverContext.GetContext();
                context.ClearModel();
                var model = context.CreateModel();

                var actionDecisions = new Dictionary<Tuple<Action, Action>, Decision>();

                foreach (Action currentAction in Enum.GetValues(typeof(Action)))
                {
                    foreach (Action opponentAction in Enum.GetValues(typeof(Action)))
                    {
                        var decision = new Decision(Domain.RealNonnegative, currentAction.ToString() + opponentAction.ToString());
                        model.AddDecisions(decision);
                        actionDecisions.Add(new Tuple<Action, Action>(currentAction, opponentAction), decision);
                    }
                }

                var actionDecisionSum = new SumTermBuilder(25);
                foreach (var decision in actionDecisions.Values)
                {
                    actionDecisionSum.Add(decision);
                }

                model.AddConstraint("probSumConst", actionDecisionSum.ToTerm() == 1.0);

                SetupRationalityConstraints(s, q.GetCurrentPlayerQValue, actionDecisions, model, "A");
                SetupRationalityConstraintsOpponent(s, q.GetOpponentQValue, actionDecisions, model, "B");

                var objectiveSum = new SumTermBuilder(10);

                //Add my terms from my Q table to objective function
                AddObjectiveFunctionTerms(s, q, actionDecisions, objectiveSum);

                model.AddGoal("MaximizeV", GoalKind.Maximize, objectiveSum.ToTerm());

                var solution = context.Solve(new SimplexDirective());

                //Console.WriteLine(solution.GetReport());

                if (solution.Quality != SolverQuality.Optimal)
                {
                    context.ClearModel();
                    return new Tuple<double, double>(1.0, 1.0);
                }

                double currentPlayerNextValue = 0.0;
                double opponentNextValue = 0.0;
                foreach (Action currentAction in Enum.GetValues(typeof(Action)))
                {
                    foreach (Action opponentAction in Enum.GetValues(typeof(Action)))
                    {
                        var pi = GetActionDecision(currentAction, opponentAction, actionDecisions);
                        var qValue = q.GetCurrentPlayerQValue(s, currentAction, opponentAction);
                        currentPlayerNextValue += pi.ToDouble() * qValue;
                        var opponentQValue = q.GetOpponentQValue(s, currentAction, opponentAction);
                        opponentNextValue += pi.ToDouble() * opponentQValue;
                    }
                }

                return new Tuple<double, double>(currentPlayerNextValue, opponentNextValue);
            }

            private static void AddObjectiveFunctionTerms(State s, CorrelatedQTable q, Dictionary<Tuple<Action, Action>, Decision> actionDecisions, SumTermBuilder objectiveSum)
            {
                foreach (Action currentAction in Enum.GetValues(typeof(Action)))
                {
                    foreach (Action opponentAction in Enum.GetValues(typeof(Action)))
                    {
                        var pi = GetActionDecision(currentAction, opponentAction, actionDecisions);
                        var qValue = q.GetCurrentPlayerQValue(s, currentAction, opponentAction);

                        objectiveSum.Add(pi * qValue);
                    }
                }

                foreach (Action currentAction in Enum.GetValues(typeof(Action)))
                {
                    foreach (Action opponentAction in Enum.GetValues(typeof(Action)))
                    {
                        var pi = GetActionDecision(currentAction, opponentAction, actionDecisions);
                        var opponentQValue = q.GetOpponentQValue(s, currentAction, opponentAction);

                        objectiveSum.Add(pi * opponentQValue);
                    }
                }
            }

            private static void SetupRationalityConstraints(State s, Func<State, Action, Action, double> getQ, Dictionary<Tuple<Action, Action>, Decision> actionDecisions, Model model, string constPrefix)
            {
                var rotatingList = new Queue<Action>();
                foreach (Action action in Enum.GetValues(typeof(Action)))
                {
                    rotatingList.Enqueue(action);
                }

                var constCount = 0;

                for (var i = 0; i < Enum.GetValues(typeof(Action)).Length; ++i)
                {
                    var contextRowSum = new SumTermBuilder(5);
                    var isContextRow = true;
                    Action contextAction = Action.North;
                    
                    foreach (Action currentAction in rotatingList)
                    {
                        var otherRowSum = new SumTermBuilder(5);

                        if (isContextRow)
                        {
                            contextAction = currentAction;
                        }

                        foreach (Action opponentAction in Enum.GetValues(typeof(Action)))
                        {
                            var pi = GetActionDecision(contextAction, opponentAction, actionDecisions);

                            if (isContextRow)
                            {
                                var qValue = getQ(s, currentAction, opponentAction);

                                contextRowSum.Add(pi*qValue);
                            }
                            else
                            {
                                var qValue = getQ(s, currentAction, opponentAction);

                                otherRowSum.Add(pi*qValue);
                            }
                        }

                        if (!isContextRow)
                        {
                            model.AddConstraint(constPrefix + "const" + constCount, contextRowSum.ToTerm() >= otherRowSum.ToTerm());
                            constCount++;
                        }

                        isContextRow = false;
                    }

                    //Rotate list
                    var elementToRotate = rotatingList.Dequeue();
                    rotatingList.Enqueue(elementToRotate);
                }
            }

            private static void SetupRationalityConstraintsOpponent(State s, Func<State, Action, Action, double> getQ, Dictionary<Tuple<Action, Action>, Decision> actionDecisions, Model model, string constPrefix)
            {
                var rotatingList = new Queue<Action>();
                foreach (Action action in Enum.GetValues(typeof(Action)))
                {
                    rotatingList.Enqueue(action);
                }

                var constCount = 0;

                for (var i = 0; i < Enum.GetValues(typeof(Action)).Length; ++i)
                {
                    var contextRowSum = new SumTermBuilder(5);
                    var isContextRow = true;
                    Action contextAction = Action.North;

                    foreach (Action opponentAction in rotatingList)
                    {
                        var otherRowSum = new SumTermBuilder(5);

                        if (isContextRow)
                        {
                            contextAction = opponentAction;
                        }

                        foreach (Action currentAction in Enum.GetValues(typeof(Action)))
                        {
                            var pi = GetActionDecision(currentAction, contextAction, actionDecisions);

                            if (isContextRow)
                            {
                                var qValue = getQ(s, currentAction, opponentAction);

                                contextRowSum.Add(pi * qValue);
                            }
                            else
                            {
                                var qValue = getQ(s, currentAction, opponentAction);

                                otherRowSum.Add(pi * qValue);
                            }
                        }

                        if (!isContextRow)
                        {
                            model.AddConstraint(constPrefix + "const" + constCount, contextRowSum.ToTerm() >= otherRowSum.ToTerm());
                            constCount++;
                        }

                        isContextRow = false;
                    }

                    //Rotate list
                    var elementToRotate = rotatingList.Dequeue();
                    rotatingList.Enqueue(elementToRotate);
                }
            }
        }

        private static Decision GetActionDecision(Action currentAction, Action opponentAction, Dictionary<Tuple<Action, Action>, Decision> actionDecisions)
        {
            var key = new Tuple<Action, Action>(currentAction, opponentAction);
            return actionDecisions[key];
        }
    }
}
