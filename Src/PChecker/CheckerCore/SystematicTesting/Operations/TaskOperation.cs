﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using PChecker.Exceptions;
using Debug = PChecker.IO.Debugging.Debug;
using SystemTasks = System.Threading.Tasks;
using Task = PChecker.Tasks.Task;

namespace PChecker.SystematicTesting.Operations
{
    /// <summary>
    /// Contains information about an asynchronous task operation
    /// that can be controlled during testing.
    /// </summary>
    [DebuggerStepThrough]
    internal sealed class TaskOperation : AsyncOperation
    {
        /// <summary>
        /// A cache from async controlled task state machine types generated by
        /// the compiler to the corresponding asynchronous methods.
        /// </summary>
        internal static readonly Dictionary<Type, MethodBase> AsyncTaskMethodCache =
            new Dictionary<Type, MethodBase>();

        /// <summary>
        /// The scheduler executing this operation.
        /// </summary>
        private readonly OperationScheduler Scheduler;

        /// <summary>
        /// The unique id of the operation.
        /// </summary>
        public override ulong Id { get; }

        /// <summary>
        /// The unique name of the operation.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Set of tasks that this operation is waiting to join. All tasks
        /// in the set must complete before this operation can resume.
        /// </summary>
        private readonly HashSet<SystemTasks.Task> JoinDependencies;

        /// <summary>
        /// The root asynchronous method that is executed by this operation.
        /// </summary>
        private MethodBase RootAsyncTaskMethod;

        /// <summary>
        /// The asynchronous method that is current executed by this operation.
        /// </summary>
        private MethodBase CurrAsyncTaskMethod;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskOperation"/> class.
        /// </summary>
        internal TaskOperation(ulong operationId, OperationScheduler scheduler)
            : base()
        {
            Scheduler = scheduler;
            Id = operationId;
            Name = $"Task({operationId})";
            JoinDependencies = new HashSet<SystemTasks.Task>();
        }

        internal void OnGetAwaiter()
        {
            IsAwaiterControlled = true;
        }

        /// <summary>
        /// Invoked when the operation is waiting to join the specified task.
        /// </summary>
        internal void OnWaitTask(SystemTasks.Task task)
        {
            Debug.WriteLine("<ScheduleDebug> Operation '{0}' is waiting for task '{1}'.", Id, task.Id);
            JoinDependencies.Add(task);
            Status = AsyncOperationStatus.BlockedOnWaitAll;
            Scheduler.ScheduleNextEnabledOperation(AsyncOperationType.Join);
            IsAwaiterControlled = false;
        }

        /// <summary>
        /// Invoked when the operation is waiting to join the specified tasks.
        /// </summary>
        internal void OnWaitTasks(IEnumerable<Task> tasks, bool waitAll)
        {
            foreach (var task in tasks)
            {
                if (!task.IsCompleted)
                {
                    Debug.WriteLine("<ScheduleDebug> Operation '{0}' is waiting for task '{1}'.", Id, task.Id);
                    JoinDependencies.Add(task.UncontrolledTask);
                }
            }

            if (JoinDependencies.Count > 0)
            {
                Status = waitAll ? AsyncOperationStatus.BlockedOnWaitAll : AsyncOperationStatus.BlockedOnWaitAny;
                Scheduler.ScheduleNextEnabledOperation(AsyncOperationType.Join);
            }

            IsAwaiterControlled = false;
        }

        /// <summary>
        /// Tries to enable the operation, if it was not already enabled.
        /// </summary>
        internal override void TryEnable()
        {
            if (Status == AsyncOperationStatus.BlockedOnWaitAll)
            {
                Debug.WriteLine("<ScheduleDebug> Try enable operation '{0}'.", Id);
                if (!JoinDependencies.All(task => task.IsCompleted))
                {
                    Debug.WriteLine("<ScheduleDebug> Operation '{0}' is waiting for all join tasks to complete.", Id);
                    return;
                }

                JoinDependencies.Clear();
                Status = AsyncOperationStatus.Enabled;
            }
            else if (Status == AsyncOperationStatus.BlockedOnWaitAny)
            {
                Debug.WriteLine("<ScheduleDebug> Try enable operation '{0}'.", Id);
                if (!JoinDependencies.Any(task => task.IsCompleted))
                {
                    Debug.WriteLine("<ScheduleDebug> Operation '{0}' is waiting for any join task to complete.", Id);
                    return;
                }

                JoinDependencies.Clear();
                Status = AsyncOperationStatus.Enabled;
            }
        }

        /// <summary>
        /// Sets the root asynchronous controlled task state machine
        /// of this operation, if it is not already set.
        /// </summary>
        internal void SetRootAsyncTaskStateMachine(Type stateMachineType)
        {
            if (RootAsyncTaskMethod is null)
            {
                // The call stack is empty, so traverse the stack trace to find the first
                // user defined method to be executed by this operation and set it as root.
                var st = new StackTrace(false);
                for (var i = st.FrameCount - 1; i > 0; i--)
                {
                    var sf = st.GetFrame(i);
                    if (TryGetUserDefinedAsyncMethodFromStackFrame(sf, stateMachineType, out var method))
                    {
                        RootAsyncTaskMethod = method;
                        break;
                    }
                }

                if (RootAsyncTaskMethod is null)
                {
                    throw new RuntimeException($"Operation '{Id}' is unable to find and set a root asynchronous method.");
                }
            }
        }

        /// <summary>
        /// Sets the asynchronous controlled task state machine with the specified type
        /// as the currently executed by this operation.
        /// </summary>
        internal void SetExecutingAsyncTaskStateMachineType(Type stateMachineType) =>
            CurrAsyncTaskMethod = GetAsyncTaskMethodComponents(stateMachineType);

        /// <summary>
        /// Checks if the operation is currently executing the root asynchronous method.
        /// </summary>
        internal bool IsExecutingInRootAsyncMethod() =>
            RootAsyncTaskMethod == CurrAsyncTaskMethod;

        /// <summary>
        /// Returns a tuple containing the name and declaring type of the asynchronous controlled
        /// task method with the specified type.
        /// </summary>
        private static MethodBase GetAsyncTaskMethodComponents(Type stateMachineType)
        {
            if (!AsyncTaskMethodCache.TryGetValue(stateMachineType, out var method))
            {
                // Traverse the stack trace to identify and return the currently executing
                // asynchronous controlled task method, and cache it for quick access later
                // in the execution or future test schedules.
                var st = new StackTrace(false);
                for (var i = 0; i < st.FrameCount; i++)
                {
                    var sf = st.GetFrame(i);
                    if (TryGetUserDefinedAsyncMethodFromStackFrame(sf, stateMachineType, out method))
                    {
                        AsyncTaskMethodCache.Add(stateMachineType, method);
                        break;
                    }
                }
            }

            return method;
        }

        /// <summary>
        /// Tries to get the user defined asynchronous method from the specified stack frame and
        /// asynchronous state machine type, if there is one, else returns false.
        /// </summary>
        private static bool TryGetUserDefinedAsyncMethodFromStackFrame(StackFrame stackFrame, Type stateMachineType, out MethodBase method)
        {
            // TODO: explore optimizations for this logic.
            var sfMethod = stackFrame.GetMethod();

            var sfMethodModuleName = sfMethod.Module.Name;
            if (sfMethodModuleName == "mscorlib.dll" ||
                sfMethodModuleName == "System.Private.CoreLib.dll" ||
                sfMethodModuleName == "Microsoft.Coyote.dll")
            {
                method = default;
                return false;
            }

            if (sfMethod.Name == "MoveNext")
            {
                // Skip the compiler generated state machine method.
                var attributes = sfMethod.DeclaringType.CustomAttributes;
                if (sfMethod.DeclaringType == stateMachineType ||
                    attributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)))
                {
                    method = default;
                    return false;
                }
            }

            method = sfMethod;
            return true;
        }
    }
}
