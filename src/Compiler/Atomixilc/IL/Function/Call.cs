﻿/*
* PROJECT:          Atomix Development
* LICENSE:          Copyright (C) Atomix Development, Inc - All Rights Reserved
*                   Unauthorized copying of this file, via any medium is
*                   strictly prohibited Proprietary and confidential.
* PURPOSE:          Call MSIL
* PROGRAMMERS:      Aman Priyadarshi (aman.eureka@gmail.com)
*/

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Atomixilc.Machine;
using Atomixilc.Attributes;
using Atomixilc.IL.CodeType;
using Atomixilc.Machine.x86;

namespace Atomixilc.IL
{
    [ILImpl(ILCode.Call)]
    internal class Call_il : MSIL
    {
        public Call_il()
            : base(ILCode.Call)
        {

        }

        /*
         * URL : https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes.Call(v=vs.110).aspx
         * Description : Calls the method indicated by the passed method descriptor.
         */
        internal override void Execute(Options Config, OpCodeType xOp, MethodBase method, Optimizer Optimizer)
        {
            var xOpMethod = (OpMethod)xOp;
            var target = xOpMethod.Value;
            var targetinfo = target as MethodInfo;

            var addressRefernce = target.FullName();
            bool NoException = target.GetCustomAttribute<NoExceptionAttribute>() != null;
            var parameters = target.GetParameters();

            int count = parameters.Length;
            int stacksize = parameters.Select(a => Helper.GetTypeSize(a.ParameterType, Config.TargetPlatform, true)).Sum();
            int returnSize = 0;
            if (targetinfo != null)
                returnSize = Helper.GetTypeSize(targetinfo.ReturnType, Config.TargetPlatform, true);

            if (!target.IsStatic)
            {
                count++;

                Type ArgType = target.DeclaringType;
                if (target.DeclaringType.IsValueType)
                    ArgType = ArgType.MakeByRefType();

                stacksize += Helper.GetTypeSize(ArgType, Config.TargetPlatform, true);
            }

            if (Optimizer.vStack.Count < count)
                throw new Exception("Internal Compiler Error: vStack.Count < expected size");

            /* The stack transitional behavior, in sequential order, is:
             * Method arguments arg1 through argN are pushed onto the stack.
             * Method arguments arg1 through argN are popped from the stack; the method call is performed with these arguments
             * and control is transferred to the method referred to by the method descriptor. When complete,
             * a return value is generated by the callee method and sent to the caller.
             * The return value is pushed onto the stack.
             */

            int index = count;
            while (index > 0)
            {
                Optimizer.vStack.Pop();
                index--;
            }

            switch (Config.TargetPlatform)
            {
                case Architecture.x86:
                    {
                        if (returnSize > 8)
                            throw new Exception(string.Format("UnImplemented '{0}' Return-type: '{1}'", msIL, targetinfo.ReturnType));

                        new Call { DestinationRef = addressRefernce };

                        switch(xOpMethod.CallingConvention)
                        {
                            case CallingConvention.StdCall: break;
                            case CallingConvention.Cdecl:
                                {
                                    new Add { DestinationReg = Register.ESP, SourceRef = "0x" + stacksize.ToString("X") };
                                }
                                break;
                            default:
                                throw new Exception(string.Format("CallingConvention '{0}' not supported", xOpMethod.CallingConvention));
                        }

                        if (!NoException)
                        {
                            new Test { DestinationReg = Register.ECX, SourceRef = "0xFFFFFFFF" };
                            new Jmp { Condition = ConditionalJump.JNZ, DestinationRef = xOp.HandlerRef };
                            Optimizer.SaveStack(xOp.HandlerPosition);
                        }

                        if (targetinfo != null && targetinfo.ReturnType != typeof(void))
                        {
                            if (returnSize == 8)
                                new Push { DestinationReg = Register.EDX };
                            new Push { DestinationReg = Register.EAX };
                            Optimizer.vStack.Push(new StackItem(targetinfo.ReturnType));
                        }

                        Optimizer.SaveStack(xOp.NextPosition);
                    }
                    break;
                default:
                    throw new Exception(string.Format("Unsupported target platform '{0}' for MSIL '{1}'", Config.TargetPlatform, msIL));
            }
        }
    }
}