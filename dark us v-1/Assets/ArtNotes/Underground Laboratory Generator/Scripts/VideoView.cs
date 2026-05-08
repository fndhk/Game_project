using System.Collections;
using UnityEngine;

namespace ArtNotes.UndergroundLaboratoryGenerator
{
    public class VideoView : MonoBehaviour
    {
        public bool StartInvisible = false;
        public float DeltaTime = .1f;
        public bool RandomOrder = false;
        public int Repeat = 500;
        private int k;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (StartInvisible)
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        transform.GetChild(i).gameObject.SetActive(false);
                    }
                }

                if (RandomOrder) StartCoroutine(InvisibleRandom());
                else
                {
                    if (StartInvisible) StartCoroutine(InvisibleOrderInverse());
                    else StartCoroutine(InvisibleOrder());
                }
            }
        }

        private IEnumerator InvisibleRandom()
        {
            for (int i = 0; i < Repeat; i++)
            {
                transform.GetChild(k).gameObject.SetActive(true);
                k = Random.Range(0, transform.childCount);
                transform.GetChild(k).gameObject.SetActive(false);
                yield return new WaitForSeconds(DeltaTime);
            }
        }

        private IEnumerator InvisibleOrder()
        {
            for (int i = 1; i < transform.childCount; i++)
            {
                transform.GetChild(i - 1).gameObject.SetActive(true);
                transform.GetChild(i).gameObject.SetActive(false);
                yield return new WaitForSeconds(DeltaTime);
            }
            transform.GetChild(transform.childCount).gameObject.SetActive(true);
        }

        private IEnumerator InvisibleOrderInverse()
        {
            for (int i = 1; i < transform.childCount; i++)
            {
                transform.GetChild(i - 1).gameObject.SetActive(false);
                transform.GetChild(i).gameObject.SetActive(true);
                yield return new WaitForSeconds(DeltaTime);
            }
        }
    }
}