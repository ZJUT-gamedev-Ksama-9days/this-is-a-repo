using UnityEngine;

namespace GravityPrototype.UI
{
    /// <summary>Null-safe child lookups by name for the code-built UI hierarchy.</summary>
    public static class UiLookup
    {
        public static T Find<T>(Transform root, string name) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform child = root.Find(name);
            return child != null ? child.GetComponent<T>() : null;
        }

        public static T FindDeep<T>(Transform root, string name) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            T[] candidates = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].gameObject.name == name)
                {
                    return candidates[i];
                }
            }

            return null;
        }
    }
}
