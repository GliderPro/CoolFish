﻿using System.Threading;
using CoolFishNS.Utilities;
using NLog;

namespace CoolFishNS.Bots.FiniteStateMachine.States
{
    /// <summary>
    ///     State which handles applying a fishing lure if we need one
    /// </summary>
    public class StateApplyLure : State
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override int Priority
        {
            get { return (int) CoolFishEngine.StatePriority.StateApplyLure; }
        }

        public override string Name
        {
            get { return "Applying lure"; }
        }

        /// <summary>
        ///     Runs this state and apply the lure.
        /// </summary>
        public override bool Run()
        {
            if (UserPreferences.Default.NoLure)
            {
                return false;
            }

            Manager.ExecuteScript("if GetWeaponEnchantInfo() then enchant = 1 else enchant = 0 end;");
            var result = Manager.GetGlobalVariable("enchant");

            if (result != "1" && PlayerInventory.HasLures())
            {
                Logger.Info(Name);

                Manager.ExecuteScript("RunMacroText(\"/use \" .. LureName);");

                Thread.Sleep(3000);
                return true;
            }
            return false;
        }
    }
}