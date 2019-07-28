using System;

namespace UnityEngine.Reflect
{
    public class StepLog : IDisposable
    {
        readonly string m_Name;
        readonly DateTime m_StartTime;
        
        public StepLog(string stepName)
        {
            m_Name = stepName;
            m_StartTime = DateTime.Now;
            
            Debug.Log("Step '" + m_Name + "' Starting...");
        }

        public void Dispose()
        {
            Debug.Log("Step '" + m_Name + "' Took : " + (DateTime.Now - m_StartTime));
        }
    }
}
