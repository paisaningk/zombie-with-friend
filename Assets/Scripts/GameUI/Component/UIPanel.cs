using Sirenix.OdinInspector;
using UnityEngine;

namespace GameUI.Component
{
    public class UIPanel : MonoBehaviour
    {
        public GameObject Panel;

        [Button]
        public void OpenPanel()
        {
            Panel.SetActive(true);
        }

        [Button]
        public void ClosePanel()
        {
            Panel.SetActive(false);
        }
    }
}
