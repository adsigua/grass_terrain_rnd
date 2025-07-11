using System.Collections.Generic;
using UnityEngine;

namespace Adobobu.Grass
{
    public class GrassManager : MonoBehaviour
    {
        public static GrassManager instance;
    
        public List<GrassRenderer> grassRenderers;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
