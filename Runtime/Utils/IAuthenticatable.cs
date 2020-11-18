using System;

namespace UnityEngine.Reflect
{
    public interface IAuthenticatable
    {
        void Start();
        void Update();
        void Login();
        void Logout();
    }
}

