﻿using System.Threading;
using CoolFishNS.Management.CoolManager.HookingLua;
using CoolFishNS.Properties;
using CoolFishNS.Utilities;

namespace CoolFishNS.Bots.FiniteStateMachine.States
{
    /// <summary>
    ///     State which handles applying the Ancient Pandaren Fishing Charm  if we need one
    /// </summary>
    public class StateUseCharm : State
    {
        public override int Priority
        {
            get { return (int) CoolFishEngine.StatePriority.StateUseCharm; }
        }

        /// <summary>
        ///     Gets a value indicating whether we [need to run] this state or not.
        /// </summary>
        /// <value>
        ///     <c>true</c> if we [need to run]; otherwise, <c>false</c>.
        /// </value>
        public override bool NeedToRun
        {
            get
            {
                string res = DxHook.Instance.ExecuteScript(Resources.NeedToRunCharm, "expires");

                return res == "1";
            }
        }

        public override string Name
        {
            get { return "Using Charm"; }
        }

        /// <summary>
        ///     Runs this state and apply the lure.
        /// </summary>
        public override void Run()
        {
            Logging.Write(Name);

            DxHook.Instance.ExecuteScript(
                "local name = GetItemInfo(85973); if name then RunMacroText(\"/use  \" .. name); end");

            Thread.Sleep(3000);
        }
    }
}