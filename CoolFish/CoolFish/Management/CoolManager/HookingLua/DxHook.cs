﻿// credits to Ryuk and highvolts from ownedcore.com

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CoolFishNS.Exceptions;
using CoolFishNS.Management.CoolManager.D3D;
using GreyMagic;
using NLog;

namespace CoolFishNS.Management.CoolManager.HookingLua
{
    /// <summary>
    ///     This class handles Hooking Endscene/Present function so that we can inject ASM if we need to do so.
    /// </summary>
    public static class DxHook
    {
        private const int CODECAVESIZE = 0x1000;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly byte[] Eraser = new byte[CODECAVESIZE];
        private static readonly object LockObject = new object();
        private static readonly Random Random = new Random();

        private static readonly string[] RegisterNames =
        {
            "ah", "al", "bh", "bl", "ch", "cl", "dh", "dl", "eax",
            "ebx", "ecx", "edx"
        };

        private static AllocatedMemory _allocatedMemory;
        private static byte[] _originalBytes;

        /// <summary>
        ///     Determine if the hook is currently applied or not
        /// </summary>
        /// <value>
        ///     <c>true</c> if the hook is applied; otherwise, <c>false</c>.
        /// </value>
        private static volatile bool _isApplied;

        private static int Inject(IEnumerable<string> asm, IntPtr address)
        {
            BotManager.Memory.Asm.Clear();
            BotManager.Memory.Asm.SetMemorySize(0x4096);

            foreach (string s in asm)
            {
                BotManager.Memory.Asm.AddLine(s);
            }

            var assembled = BotManager.Memory.Asm.Assemble();
            BotManager.Memory.Asm.Inject((uint) address);

            return assembled.Length;
        }


        /// <summary>
        ///     Apply the DirectX function hook to the WoW process
        /// </summary>
        /// <returns>true if it applied correctly. Otherwise, false</returns>
        internal static bool Apply()
        {
            lock (LockObject)
            {
                try
                {
                    //Lets check if we are already hooked.
                    if (_isApplied)
                    {
                        return true;
                    }
                    if (BotManager.Memory == null || BotManager.Memory.Process.HasExited)
                    {
                        return false;
                    }

                    _allocatedMemory = BotManager.Memory.CreateAllocatedMemory(CODECAVESIZE + 0x1000 + 0x4 + 0x4);

                    var detourFunctionPointer = Offsets.Addresses["CGWorldFrame__Render"];

                    // store original bytes
                    _originalBytes = BotManager.Memory.ReadBytes(detourFunctionPointer, 6);
                    if (_originalBytes[0] == 0xE9)
                    {
                        MessageBox.Show(
                            "It seems CoolFish might have crashed before it could clean up after itself. Please restart WoW and reattach the bot.");
                        return false;
                    }
                
                    _allocatedMemory.WriteBytes("codeCavePtr", Eraser);
                    _allocatedMemory.WriteBytes("injectedCode", Eraser);
                    _allocatedMemory.Write("addressInjection", 0);
                    _allocatedMemory.Write("returnInjectionAsm", 0);

                    var asm = new List<string>
                    {
                        "pushad", // save registers to the stack
                        "pushfd",
                        "mov eax, [" + _allocatedMemory["addressInjection"] + "]",
                        "test eax, eax", // Test if you need launch injected code
                        "je @out",
                        "mov eax, [" + _allocatedMemory["addressInjection"] + "]",
                        "call eax", // Launch Function
                        "mov [" + _allocatedMemory["returnInjectionAsm"] + "], eax", // Copy pointer return value
                        "mov edx, " + _allocatedMemory["addressInjection"], // Enter value 0 of so we know we are done
                        "mov ecx, 0",
                        "mov [edx], ecx",
                        "@out:", // Close function
                        "popfd", // load reg
                        "popad"
                    };

                    asm = AddRandomAsm(asm);

                    // injected code
                    int sizeAsm = Inject(asm, _allocatedMemory["injectedCode"]);

                    // copy and save original instructions
                    BotManager.Memory.WriteBytes(_allocatedMemory["injectedCode"] + sizeAsm, _originalBytes);

                    asm.Clear();
                    asm.Add("jmp " + (detourFunctionPointer + _originalBytes.Length));

                    // create jump back stub
                    Inject(asm, _allocatedMemory["injectedCode"] + sizeAsm + _originalBytes.Length);

                    // create hook jump
                    asm.Clear();
                    asm.Add("jmp " + _allocatedMemory["injectedCode"]);
                    asm.Add("nop");

                    Inject(asm, detourFunctionPointer);
                    _isApplied = true;
                }
                catch
                {
                    _isApplied = false;
                    if (_allocatedMemory != null)
                    {
                        _allocatedMemory.Dispose();
                    }
                    throw;
                }

                return true;
            }
        }

        /// <summary>
        ///     Restore the original Endscene function and remove the function hook
        /// </summary>
        internal static void Restore()
        {
            try
            {
                lock (LockObject)
                {
                    //Lets check if were hooked
                    if (!_isApplied)
                    {
                        return;
                    }
                    if (BotManager.Memory == null || BotManager.Memory.Process.HasExited)
                    {
                        _isApplied = false;
                        return;
                    }

                    // Restore original endscene:
                    BotManager.Memory.WriteBytes(Offsets.Addresses["CGWorldFrame__Render"], _originalBytes);

                    if (_allocatedMemory != null)
                    {
                        _allocatedMemory.Dispose();
                    }

                    _isApplied = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception thrown while restoring original DirectX function", ex);
            }
        }

        /// <summary>
        ///     Inject x86 assembly into the target process and execute it
        /// </summary>
        /// <param name="asm">Assembly code to inject</param>
        /// <returns>true if the code was injected. Otherwise false.</returns>
        /// <exception cref="HookNotAppliedException">Thrown when the required hook has not been applied</exception>
        private static void InjectAndExecute(IEnumerable<string> asm)
        {
            lock (LockObject)
            {
                if (!_isApplied)
                {
                    throw new HookNotAppliedException("Tried to inject code when the Hook was not applied");
                }
                //Lets Inject the passed ASM
                Inject(asm, _allocatedMemory["codeCavePtr"]);


                _allocatedMemory.Write("addressInjection", _allocatedMemory["codeCavePtr"]);

                Stopwatch timer = Stopwatch.StartNew();

                while (_allocatedMemory.Read<int>("addressInjection") > 0)
                {
                    Thread.Sleep(1);
                    if (timer.ElapsedMilliseconds >= 10000)
                    {
                        throw new CodeInjectionFailedException("Failed to inject code after 10 seconds. Last Error: " + Marshal.GetLastWin32Error());
                    }
                } // Wait to launch code

                _allocatedMemory.WriteBytes("codeCavePtr", Eraser);
            }
        }

        /// <summary>
        ///     Execute custom Lua script into the Wow process
        /// </summary>
        /// <param name="command">Lua code to execute</param>
        /// <exception cref="HookNotAppliedException">Thrown when the required hook has not been applied</exception>
        public static void ExecuteScript(string command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            ExecuteScript(command, new string[0]);
        }

        /// <summary>
        ///     Execute custom LUA code into the Wow process and retrieve a global LUA variable (via GetLocalizedText) all in one
        ///     method
        /// </summary>
        /// <param name="command">Lua code to execute</param>
        /// <param name="returnVariableName">Name of the global LUA variable to return</param>
        /// <exception cref="HookNotAppliedException">Thrown when the required hook has not been applied</exception>
        public static string ExecuteScript(string command, string returnVariableName)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }
            if (returnVariableName == null)
            {
                throw new ArgumentNullException("returnVariableName");
            }

            return ExecuteScript(command, new[] {returnVariableName})[returnVariableName];
        }

        /// <summary>
        ///     Execute custom LUA code into the Wow process and retrieve a list of global LUA variables (via GetLocalizedText) all
        ///     in one method
        /// </summary>
        /// <param name="executeCommand">Lua code to execute</param>
        /// <param name="commands">Collection of string name of global lua variables to retrieve</param>
        /// <returns>values of the variables to retrieve</returns>
        /// <exception cref="HookNotAppliedException">Thrown when the required hook has not been applied</exception>
        public static Dictionary<string, string> ExecuteScript(string executeCommand, IEnumerable<string> commands)
        {
            var returnDict = new Dictionary<string, string>();

            if (executeCommand == null)
            {
                throw new ArgumentNullException("executeCommand");
            }
            if (commands == null)
            {
                throw new ArgumentNullException("commands");
            }

            List<string> enumerable = commands as List<string> ?? commands.ToList();

            var builder = new StringBuilder(enumerable.Count);

            if (enumerable.Any())
            {
                foreach (string s in enumerable.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    builder.Append(s);
                    builder.Append('\0');
                    returnDict[s] = string.Empty;
                }
            }
            else
            {
                builder.Append('\0');
            }


            int commandSpace = Encoding.UTF8.GetBytes(builder.ToString()).Length;
            commandSpace += commandSpace%4;
            int commandExecuteSpace = Encoding.UTF8.GetBytes(executeCommand).Length + 1;
            commandExecuteSpace += commandExecuteSpace%4;
            int returnAddressSpace = enumerable.Count == 0 ? 0x4 : enumerable.Count*0x4;

            AllocatedMemory mem =
                BotManager.Memory.CreateAllocatedMemory(commandSpace + commandExecuteSpace + returnAddressSpace +
                                                        0x4);

            try
            {
                mem.WriteBytes("command", Encoding.UTF8.GetBytes(builder.ToString()));
                mem.WriteBytes("commandExecute", Encoding.UTF8.GetBytes(executeCommand));
                mem.WriteBytes("returnVarsPtr", enumerable.Count != 0 ? new byte[enumerable.Count*0x4] : new byte[0x4]);
                mem.Write("returnVarsNamesPtr", mem["command"]);


                InternalExecute(mem["commandExecute"], mem["returnVarsNamesPtr"], enumerable.Count, mem["returnVarsPtr"]);


                if (enumerable.Any())
                {
                    byte[] address = BotManager.Memory.ReadBytes(mem["returnVarsPtr"], enumerable.Count*4);

                    Parallel.ForEach(enumerable, // source collection
                        () => 0, // method to initialize the local variable
                        (value, loop, offset) => // method invoked by the loop on each iteration
                        {
                            var retnByte = new List<byte>();
                            var dwAddress = new IntPtr(BitConverter.ToInt32(address, offset));

                            if (dwAddress != IntPtr.Zero)
                            {
                                var buf = BotManager.Memory.Read<byte>(dwAddress);
                                while (buf != 0)
                                {
                                    retnByte.Add(buf);
                                    dwAddress = dwAddress + 1;
                                    buf = BotManager.Memory.Read<byte>(dwAddress);
                                }
                            }
                            returnDict[value] = Encoding.UTF8.GetString(retnByte.ToArray());
                            offset += 0x4; //modify local variable 
                            return offset; // value to be passed to next iteration
                        },
                        finalResult => { }
                        );
                }
            }
            finally
            {
                mem.Dispose();
            }

            return returnDict;
        }

        /// <summary>
        ///     Retrieve a custom global variable in the Lua scope
        /// </summary>
        /// <param name="command">String name of variable to retrieve</param>
        /// <returns>value of the variable to retrieve</returns>
        /// <exception cref="HookNotAppliedException">Thrown when the required hook has not been applied</exception>
        public static string GetLocalizedText(string command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }
            string result = GetLocalizedText(new[] {command})[command];

            if (Logger.IsTraceEnabled)
            {
                Logger.Trace("GLT result: " + result);
            }
            return result;
        }

        /// <summary>
        ///     Retrieve global LUA variables from the WoW process
        /// </summary>
        /// <param name="commands">String names of variables to retrieve</param>
        /// <returns>values of the variables to retrieve</returns>
        /// <exception cref="HookNotAppliedException">Thrown when the required hook has not been applied</exception>
        public static Dictionary<string, string> GetLocalizedText(IEnumerable<string> commands)
        {
            var returnDict = new Dictionary<string, string>();

            if (commands == null)
            {
                throw new ArgumentNullException("commands");
            }

            List<string> enumerable = commands.ToList();

            if (!enumerable.Any())
            {
                return returnDict;
            }
            var builder = new StringBuilder(enumerable.Count);

            foreach (string s in enumerable.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                builder.Append(s);
                builder.Append('\0');
                returnDict[s] = string.Empty;
            }


            int commandSpace = Encoding.UTF8.GetBytes(builder.ToString()).Length;
            commandSpace += commandSpace%4;
            int returnAddressSpace = enumerable.Count == 0 ? 0x4 : enumerable.Count*0x4;

            AllocatedMemory mem =
                BotManager.Memory.CreateAllocatedMemory(commandSpace + returnAddressSpace + 0x4);

            try
            {
                mem.WriteBytes("command", Encoding.UTF8.GetBytes(builder.ToString()));
                mem.WriteBytes("returnVarsPtr", new byte[enumerable.Count*0x4]);
                mem.Write("returnVarsNamesPtr", mem["command"]);


                InternalExecute(IntPtr.Zero, mem["returnVarsNamesPtr"], enumerable.Count, mem["returnVarsPtr"]);

                if (enumerable.Any())
                {
                    byte[] address = BotManager.Memory.ReadBytes(mem["returnVarsPtr"], enumerable.Count*4);

                    Parallel.ForEach(enumerable, // source collection
                        () => 0, // method to initialize the local variable
                        (value, loop, offset) => // method invoked by the loop on each iteration
                        {
                            var retnByte = new List<byte>();
                            var dwAddress = new IntPtr(BitConverter.ToInt32(address, offset));

                            if (dwAddress != IntPtr.Zero)
                            {
                                var buf = BotManager.Memory.Read<byte>(dwAddress);
                                while (buf != 0)
                                {
                                    retnByte.Add(buf);
                                    dwAddress = dwAddress + 1;
                                    buf = BotManager.Memory.Read<byte>(dwAddress);
                                }
                            }
                            returnDict[value] = Encoding.UTF8.GetString(retnByte.ToArray());
                            offset += 0x4; //modify local variable 
                            return offset; // value to be passed to next iteration
                        },
                        finalResult => { }
                        );
                }
            }
            finally
            {
                mem.Dispose();
            }

            return returnDict;
        }

        private static void InternalExecute(IntPtr executeCommandPtr, IntPtr returnVarsNamesPtr, int numberOfReturnVars, IntPtr returnVarsPtr)
        {
            var asm = new List<string>();

            if (executeCommandPtr != IntPtr.Zero)
            {
                // We want to call ExecuteScriptBuffer
                asm.AddRange(new[]
                {
                    "mov eax, " + executeCommandPtr,
                    "push 0",
                    "push eax",
                    "push eax",
                    "call " + Offsets.Addresses["FrameScript_ExecuteBuffer"],
                    "add esp, 0xC"
                });
            }
            if (returnVarsNamesPtr != IntPtr.Zero &&
                returnVarsPtr != IntPtr.Zero)
            {
                // We want to call GetLocalizedText
                asm.AddRange(new[]
                {
                    "mov esi, 0",
                    "mov edi, [" + returnVarsNamesPtr + "]",
                    "mov edx, " + returnVarsPtr,
                    "@start:",
                    "cmp esi, " + numberOfReturnVars,
                    "je @leave",
                    "push edx",
                    "push 0",
                    "push 0",
                    "push -1",
                    "push edi",
                    "call " + Offsets.Addresses["FrameScript_GetText"],
                    "add esp, 10h",
                    "pop edx",
                    "mov [edx], eax", // Copy pointer return value
                    "inc esi",
                    "add edx, 4",
                    "@start_loop:",
                    "inc edi",
                    "mov al, [edi]",
                    "test al, al",
                    "jne @start_loop",
                    "inc edi",
                    "jmp @start",
                    "@leave:"
                });
            }

            asm.Add("retn");
            InjectAndExecute(asm);
        }


        private static List<string> AddRandomAsm(IEnumerable<string> asm)
        {
            var randomizedList = new List<string>();

            foreach (string a in asm)
            {
                int ranNum = Random.Next(0, 7);
                if (ranNum == 0)
                {
                    randomizedList.Add("nop");
                    if (Random.Next(2) == 1)
                    {
                        randomizedList.Add("nop");
                    }
                }
                else if (ranNum <= 5)
                {
                    randomizedList.Add(GetRandomMov());
                    if (Random.Next(5) == 1)
                    {
                        randomizedList.Add("nop");
                    }
                }
                randomizedList.Add(a);
            }


            return randomizedList;
        }

        private static string GetRandomMov()
        {
            int ranNum = Random.Next(0, RegisterNames.Length);
            return string.Format("mov {0}, {1}", RegisterNames[ranNum], RegisterNames[ranNum]);
        }
    }
}