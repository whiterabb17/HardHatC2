﻿using System;
using System.Collections.Generic;

namespace TeamServer.Models.Extras
{
    [Serializable]
    public class C2TaskMessage
    {
        public List<string> PathMessage { get; set; } 
        public byte[] TaskData { get; set;}
    }
}
