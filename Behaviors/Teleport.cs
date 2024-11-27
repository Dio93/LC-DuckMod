using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DuckMod.Behaviors
{
    internal class Teleport
    {
        public int id;
        public Vector3 entrance;
        public Vector3 exit;

        public Teleport(int id)
        {
            this.id = id;
            entrance = Vector3.zero;
            exit = Vector3.zero;
        }
    }
}
