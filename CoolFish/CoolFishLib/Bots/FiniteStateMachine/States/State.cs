﻿using System;
using System.Collections.Generic;
using CoolFishNS.Management;

namespace CoolFishNS.Bots.FiniteStateMachine.States
{
    /// <summary>
    ///     Abstract State class that all states should inherit from and implement the required members
    /// </summary>
    public abstract class State : IComparable<State>, IComparer<State>
    {
        protected readonly IScriptManager Manager;

        /// <summary>
        ///     Creates a new instance of this state
        /// </summary>
        protected State()
        {
            Manager = new ScriptManagerFactory().GetInstance();
        }

        /// <summary>
        ///     Priority order of this state (higher number = higher priority)
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        ///     Name of the state. returns the Class name by default
        /// </summary>
        public virtual string Name
        {
            get { return GetType().Name; }
        }

        #region IComparable<State> Members

        public int CompareTo(State other)
        {
            // We want the highest first.
            // int, by default, chooses the lowest to be sorted
            // at the bottom of the list. We want the opposite.
            return -Priority.CompareTo(other.Priority);
        }

        #endregion

        #region IComparer<State> Members

        public int Compare(State x, State y)
        {
            return -x.Priority.CompareTo(y.Priority);
        }

        #endregion

        /// <summary>
        ///     Runs the current state
        /// </summary>
        public abstract bool Run();
    }
}