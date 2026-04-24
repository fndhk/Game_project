using UnityEngine;

namespace ArtNotes.UndergroundLaboratoryGenerator
{
    [RequireComponent(typeof(BoxCollider))]
    public class Cell : MonoBehaviour
    {
        [HideInInspector]
        public BoxCollider TriggerBox;
        public GameObject[] Exits;

        private void Awake()
        {
            TriggerBox = GetComponent<BoxCollider>();
            TriggerBox.isTrigger = true;
        }
    }
}