using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

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

