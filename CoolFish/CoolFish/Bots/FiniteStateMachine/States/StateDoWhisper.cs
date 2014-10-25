﻿using System.Collections.Generic;
using System.Media;
using System.Threading;
using CoolFishNS.Management.CoolManager.HookingLua;
using NLog;

namespace CoolFishNS.Bots.FiniteStateMachine.States
{
    /// <summary>
    ///     This state is run if we want to be notified by whispers in the game and one occurs.
    /// </summary>
    public class StateDoWhisper : State
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override int Priority
        {
            get { return (int) CoolFishEngine.StatePriority.StateDoWhisper; }
        }

        /// <summary>
        ///     Get the message and author of the whisper and display it to the user and play a sound.
        /// </summary>
        public override bool Run()
        {
            if (DxHook.GetLocalizedText("NewMessage") == "1")
            {
                Dictionary<string, string> result = DxHook.ExecuteScript("NewMessage = 0;", new[] {"Message", "Author"});

                Logger.Info("Whisper from: " + result["Author"] + " Message: " + result["Message"]);

                SystemSounds.Asterisk.Play();

                Thread.Sleep(3000);

                SystemSounds.Asterisk.Play();
                return true;
            }
            return false;
        }
    }
}