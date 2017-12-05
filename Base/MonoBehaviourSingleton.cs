using System;
using UnityEngine;

namespace UnityTools.Base
{
    /// <summary>
    ///     This is a generic Singleton implementation for Monobehaviours.
    ///     Create a derived class where the type T is the script you want to "Singletonize"
    ///     Upon loading it will call DontDestroyOnLoad on the gameobject where this script is contained
    ///     so it persists upon scene changes.
    /// </summary>
    /// <remarks>
    ///     DO NOT REDEFINE Awake() Start() or OnDestroy() in derived classes. EVER.
    ///     Instead, use protected virtual methods:
    ///     SingletonAwakened()
    ///     SingletonStarted()
    ///     SingletonDestroyed()
    ///     to perform the initialization/cleanup: those methods are guaranteed to only be called once in the
    ///     entire lifetime of the MonoBehaviour
    /// </remarks>
    public class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        ///     <c>true</c> if this Singleton Awake() method has already been called by Unity; otherwise, <c>false</c>.
        /// </summary>
        public static bool IsAwakened  { get; private set; }

        /// <summary>
        ///     <c>true</c> if this Singleton Start() method has already been called by Unity; otherwise, <c>false</c>.
        /// </summary>
        public static bool IsStarted   { get; private set; }

        /// <summary>
        ///     <c>true</c> if this Singleton OnDestroy() method has already been called by Unity; otherwise, <c>false</c>.
        /// </summary>
        public static bool IsDestroyed { get; private set; }

        /// <summary>
        ///     Global access point to the unique instance of this class.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (IsDestroyed) return null;

                    _instance = FindExistingInstance() ?? CreateNewInstance();
                }
                return _instance;
            }
        }

        #region Singleton Implementation

        /// <summary>
        ///     Holds the unique instance for this class
        /// </summary>
        private static T _instance;

        /// <summary>
        ///     Finds an existing instance of this singleton in the scene.
        /// </summary>
        private static T FindExistingInstance()
        {
            T[] existingInstances = FindObjectsOfType<T>();

            // No instance found
            if (existingInstances == null || existingInstances.Length == 0) return null;

            return existingInstances[0];
        }

        /// <summary>
        ///     If no instance of the T MonoBehaviour exists, creates a new GameObject in the scene
        ///     and adds T to it.
        /// </summary>
        private static T CreateNewInstance()
        {
            var containerGO = new GameObject("__" + typeof(T).Name + " (Singleton)");
            return containerGO.AddComponent<T>();
        }

        #endregion

        #region Singleton Life-time Management

        /// <summary>
        ///     Unity3D Awake method.
        /// </summary>
        /// <remarks>
        ///     This method will only be called once even if multiple instances of the
        ///     singleton MonoBehaviour exist in the scene.
        ///     You can override this method in derived classes to customize the initialization of your MonoBehaviour
        /// </remarks>
        protected virtual void SingletonAwakened() {}

        /// <summary>
        ///     Unity3D Start method.
        /// </summary>
        /// <remarks>
        ///     This method will only be called once even if multiple instances of the
        ///     singleton MonoBehaviour exist in the scene.
        ///     You can override this method in derived classes to customize the initialization of your MonoBehaviour
        /// </remarks>
        protected virtual void SingletonStarted() {}

        /// <summary>
        ///     Unity3D OnDestroy method.
        /// </summary>
        /// <remarks>
        ///     This method will only be called once even if multiple instances of the
        ///     singleton MonoBehaviour exist in the scene.
        ///     You can override this method in derived classes to customize the initialization of your MonoBehaviour
        /// </remarks>
        protected virtual void SingletonDestroyed() {}


        /// <summary>
        ///     If a duplicated instance of a Singleton MonoBehaviour is loaded into the scene
        ///     this method will be called instead of SingletonAwakened(). That way you can customize
        ///     what to do with repeated instances.
        /// </summary>
        /// <remarks>
        ///     The default approach is delete the duplicated MonoBehaviour
        /// </remarks>
        protected virtual void NotifyInstanceRepeated()
        {
            Component.Destroy(this.GetComponent<T>());
        }

        #endregion

        #region Unity3d Messages - DO NOT OVERRRIDE NOR IMPLEMENT THESE METHODS in child classes!

        void Awake()
        {
            T thisInstance = this.GetComponent<T>();

            // Initialize the singleton
            if (_instance == null)
            {
                _instance = thisInstance;
                DontDestroyOnLoad(_instance.gameObject);

            }
            // She singleton if was already living in a GameObject
            else if(thisInstance != _instance)
            {
                PrintWarn(string.Format(
                    "Found a duplicated instance of a Singleton with type {0} in the GameObject {1} that will be ignored",
                    this.GetType(), this.gameObject.name));

                NotifyInstanceRepeated();

                return;
            }


            if(!IsAwakened)
            {
                PrintLog(string.Format(
                    "Awake() Singleton with type {0} in the GameObject {1}",
                    this.GetType(), this.gameObject.name));

                SingletonAwakened();
                IsAwakened = true;
            }

        }

        void Start()
        {
            // do not start it twice
            if(IsStarted) return;

            PrintLog(string.Format(
                "Start() Singleton with type {0} in the GameObject {1}",
                this.GetType(), this.gameObject.name));

            SingletonStarted();
            IsStarted = true;
        }

        void OnDestroy()
        {
            // Here we are dealing with a duplicate so we don't need to shut the singleton down
            if (this != _instance) return;

            // Flag set when Unity sends the message OnDestroy to this Component.
            // This is needed because there is a chance that the GameObject A holding this singleton
            // is destroyed before some other GameObject B that also access this singleton when B is being destroyed.
            // If that happens as the singleton instance whould be null a new singleton would be created 
            // including a brand new GO to which the singleton instance is attached to.
            // As this is happening during the Unity app shutdown for some reason the newly created GO
            // is kept in the scene when you exist play mode, so this prevent filling your scene with remmants 
            // of singleton GameObjects
            IsDestroyed = true;

            PrintLog(string.Format(
                "Destroy() Singleton with type {0} in the GameObject {1}",
                this.GetType(), this.gameObject.name));
            SingletonDestroyed();
        }

        #endregion

        #region Debug Methods (available in child classes)

        [Header("Debug")]
        /// <summary>
        ///  Set this to true either by code or in the inspector to print trace log messages
        /// </summary>
        public bool PrintTrace = false;

        protected void PrintLog(string str, params object[] args)
        {
            Print(UnityEngine.Debug.Log, PrintTrace, str, args);
        }

        protected void PrintWarn(string str, params object[] args)
        {
            Print(UnityEngine.Debug.LogWarning, PrintTrace, str, args);
        }

        protected void PrintError(string str, params object[] args)
        {
            Print(UnityEngine.Debug.LogError, true, str, args);
        }

        private void Print(Action<string> printFunc, bool doPrint, string str, params object[] args)
        {
            if(doPrint)
            {
                printFunc(string.Format(
                    "<b>[{0}][{1}] {2} </b>",
                    Time.frameCount,
                    this.GetType().Name.ToUpper(),
                    string.Format(str, args)
                    )
                );
            }
        }

        #endregion
    }
}
