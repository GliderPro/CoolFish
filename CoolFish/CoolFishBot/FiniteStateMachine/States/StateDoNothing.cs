﻿using CoolFishNS.Management.CoolManager.Objects;
using NLog;

namespace CoolFishBotNS.FiniteStateMachine.States
{
    /// <summary>
    ///     This state is run if we are moving or get into combat. This prevents the bot from trying to do anything when it is
    ///     unable to do so.
    /// </summary>
    public class StateDoNothing : State
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override int Priority
        {
            get { return (int) CoolFishEngine.StatePriority.StateDoNothing; }
        }

        /// <summary>
        ///     Do "nothing"
        /// </summary>
        public override bool Run()
        {
            Logger.Trace("[DoNothingState]");

            var me = ObjectManager.Me;
            return me == null || me.Combat || me.Speed > 0;
        }
    }
}