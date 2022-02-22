using System;
using System.Diagnostics;
using Unity.Profiling;
using Unity.Reflect.ActorFramework;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("9ad467c4-7be5-4b3c-8e58-07ebb35c8683", true)]
    public class MemoryMonitoringActor
    {
        const long k_Megabytes = 1024L * 1024L;

#pragma warning disable 649
        Settings m_Settings;
        NetComponent m_Net;
        NetOutput<MemoryStateChanged> m_MemoryStateChangedOutput;
        PipeOutput<CleanAfterCriticalMemory> m_CleanAfterCriticalMemoryOutput;
#pragma warning restore 649

        long m_SystemMemorySize;
        
        long m_CriticalMemoryThreshold;
        long m_HighMemoryThreshold;
        long m_MediumMemoryThreshold;
        bool m_IsMemoryUsageTooHigh;
        
        TimeSpan m_LastSignalElapsed;
        long m_FrameTotalMemory;
        long m_FrameUsedMemory;
        
        ProfilerRecorder m_TotalReservedMemoryRecorder;
        ProfilerRecorder m_TotalUsedMemoryRecorder;

        bool m_IsCleaningPipeRunning;

        public void Initialize()
        {
            if (m_Settings.OverrideMemoryLimit)
                m_SystemMemorySize = m_Settings.OverridenMemorySizeMB * k_Megabytes;
            else
                m_SystemMemorySize = SystemInfo.systemMemorySize * k_Megabytes;
            m_CriticalMemoryThreshold = m_SystemMemorySize - GetReservedMemoryForOS(m_SystemMemorySize);

            ComputeMemoryUsageLimit();
            Application.lowMemory += SendCriticalMemory;
            m_TotalReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            m_TotalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        }

        public void Shutdown()
        {
            Application.lowMemory -= SendCriticalMemory;
            m_TotalReservedMemoryRecorder.Dispose();
            m_TotalUsedMemoryRecorder.Dispose();
        }

        public TickResult Tick(TimeSpan endTime)
        {
            m_Net.Tick(TimeSpan.MaxValue);
            
            var currentTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            if (currentTime > m_LastSignalElapsed + TimeSpan.FromMilliseconds(200))
            {
                m_LastSignalElapsed = currentTime;
                UpdateMemoryUsage();
                SendMessage();
            }

            return TickResult.Yield;
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;

            if (fieldName == nameof(Settings.OverrideMemoryLimit))
                m_Settings.OverrideMemoryLimit = (bool)newValue;
            else if (fieldName == nameof(Settings.OverridenMemorySizeMB))
                m_Settings.OverridenMemorySizeMB = (int)newValue;

            if (m_Settings.OverrideMemoryLimit)
                m_SystemMemorySize = m_Settings.OverridenMemorySizeMB * k_Megabytes;
            else
                m_SystemMemorySize = SystemInfo.systemMemorySize * k_Megabytes;
            
            m_CriticalMemoryThreshold = m_SystemMemorySize - GetReservedMemoryForOS(m_SystemMemorySize);
            ComputeMemoryUsageLimit();
        }

        void SendCriticalMemory()
        {
            UpdateMemoryUsage();
            m_CriticalMemoryThreshold = (long)(m_FrameTotalMemory * 0.95);
            ComputeMemoryUsageLimit();
            SendMessage();
        }

        void SendMessage()
        {
            //if (m_IsCleaningPipeRunning)
            //    return;

            //var prevTooHigh = m_IsMemoryUsageTooHigh;
            //var memUsageRatio = m_FrameTotalMemory / (float)m_CriticalMemoryThreshold;

            //if (memUsageRatio >= 0.99f)
            //    m_IsMemoryUsageTooHigh = true;
            //else if (m_IsMemoryUsageTooHigh &&
            //    m_FrameTotalMemory / (float)m_CriticalMemoryThreshold < 0.98f &&
            //    m_FrameUsedMemory / (float)m_FrameTotalMemory < 0.80f)
            //{
            //    m_IsMemoryUsageTooHigh = false;
            //}

            var msg = new MemoryStateChanged(
                m_CriticalMemoryThreshold,
                m_HighMemoryThreshold,
                m_MediumMemoryThreshold,
                m_FrameUsedMemory,
                m_FrameTotalMemory,
                false);
            
            m_MemoryStateChangedOutput.SendCritical(msg);

            //if (!prevTooHigh && m_IsMemoryUsageTooHigh)
            //{
            //    m_IsCleaningPipeRunning = true;
            //    var pipe = m_CleanAfterCriticalMemoryOutput.PushCritical(this, (object)null, (object)null, new CleanAfterCriticalMemory());
            //    pipe.Success((self, ctx, isRunning, msg) => m_IsCleaningPipeRunning = false);
            //    pipe.Failure((self, ctx, isRunning, ex) => m_IsCleaningPipeRunning = false);
            //}
        }

        void UpdateMemoryUsage()
        {
            m_FrameTotalMemory = m_TotalReservedMemoryRecorder.LastValue;
            m_FrameUsedMemory = m_TotalUsedMemoryRecorder.LastValue;
        }

        void ComputeMemoryUsageLimit()
        {
            m_HighMemoryThreshold = (long)(m_CriticalMemoryThreshold * m_Settings.HighMemoryThresholdMultiplier);
            m_MediumMemoryThreshold = (long)(m_CriticalMemoryThreshold * m_Settings.MediumMemoryThresholdMultiplier);
        }

        long GetReservedMemoryForOS(long systemMemorySize)
        {
            // Rough estimation of OS memory requirements
            long reserved;
            if (systemMemorySize <= 8192 * k_Megabytes)
                reserved = (long)(systemMemorySize * 0.5);
            else if (systemMemorySize <= 16384 * k_Megabytes)
                reserved = (long)(systemMemorySize * 0.3);
            else
                reserved = (long)(systemMemorySize * 0.2);

            return (long)(reserved * m_Settings.CriticalMemoryThresholdMultiplier);
        }

        public class Settings : ActorSettings
        {
            public bool OverrideMemoryLimit;
            public int OverridenMemorySizeMB;
            public float MediumMemoryThresholdMultiplier = 0.6f;
            public float HighMemoryThresholdMultiplier = 0.8f;
            public float CriticalMemoryThresholdMultiplier = 1.0f;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
