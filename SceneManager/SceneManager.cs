using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityTools.Base;

namespace UnityTools.SceneManager
{
    /// <summary>
    ///     Args for events notifying that a scene has finished loading.
    /// </summary>
    /// <remarks>
    ///     We make the class inherit from EventArgs so the scene manager can raise
    ///     events, howerver they are used as parameters in serveral Action<SceneLoadingArgs>
    ///     callbacks so this class is not only used in events.
    /// </remarks>
    public class SceneLoadingArgs : EventArgs
    {
        // Register the load start time and when the scene finished loading.
        private long _loadStartTime, _loadLoadEndTime;
        private static int NextId = 1;
        private int _id;
        private Action<SceneLoadingArgs> _loadSceneStepCallback;
        private UnityEngine.AsyncOperation _asyncOp;


        public SceneLoadingArgs(string sceneName,
                                bool loadAdditive,
                                bool loadAsync,
                                Action<SceneLoadingArgs> loadSceneStepCallback)

        {
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentNullException ("sceneName");

            _id = NextId++;
            _loadStartTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            _loadSceneStepCallback = loadSceneStepCallback;

            SceneName = sceneName;
            IsSceneLoadingAdditive = loadAdditive;
            IsSceneLoadingAsync = loadAsync;
        }

        internal void SaveAsyncOperationReference(UnityEngine.AsyncOperation asyncOp)
        {
            _asyncOp = asyncOp;
        }

        /// <summary>
        ///     Name of the scene being loaded
        /// </summary>
        public string SceneName {get; private set; }

        /// <summary>
        ///     True if the scene was loaded additive: contents of the newly scene are added to the scene
        ///     currently playing.
        /// </summary>
        public bool IsSceneLoadingAdditive {get; private set; }

        /// <summary>
        ///     True if the scene was loaded asyncronously
        /// </summary>
        public bool IsSceneLoadingAsync {get; private set; }

        /// <summary>
        ///     Loading progress percent. Range is [0,1]
        /// </summary>
        public float LoadingProgress { get; private set;}

        /// <summary>
        ///   True if the scene was loaded in memory. 
        ///   Note: for async loaded scenes, the scene may be loaded in memory but not activated
        ///   meaning, the scene contents are not available yet in the scene tree.
        /// </summary>
        public bool IsSceneInMemory  {get; private set; }

        /// <summary>
        ///   True if the scene was loaded and ready to use.
        /// </summary>
        public bool IsSceneLoaded { get { return IsSceneInMemory && IsSceneActivated; } }

        /// <summary>
        ///   True if the scene is active in unity. Call ActivateScene() to actually activate a loaded scene
        /// </summary>
        public bool IsSceneActivated {get; private set;}

        /// <summary>
        ///     If <c>true</c> the scene will activate automatically (loaded in the scene tree)
        ///     after loading.
        /// </summary>
        /// <default>true</default>
        public bool IsSceneActivatedOnLoad
        {
            get
            {
                return _asyncOp == null? true : _asyncOp.allowSceneActivation;
            }
        }

        /// <summary>
        ///     Call this method for scenes loaded asynchronously to activate the scene in unity and
        ///     load its contents into the scene tree.
        /// </summary>
        /// <remarks>
        ///     You can call this method even if the scene has not finished loading, in which case
        ///     the scene will be activated after is finished loading, effectively behaving as if
        ///     the IsSceneActivatedOnLoad flag were set to true.
        /// </remarks>
        public void ActivateScene()
        {
            if (_asyncOp == null) return;
            _asyncOp.allowSceneActivation = true;
        }

        /// <summary>
        ///     Time that took the scene to load
        /// </summary>
        public long ElapsedMsecs
        {
            get
            {
                if (IsSceneInMemory) return _loadLoadEndTime - _loadStartTime;
                return (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - _loadStartTime;
            }
        }

        /// <summary>
        ///     Time when the scene finished loading
        /// </summary>
        public long LoadEndTime
        {
            get
            {
                return IsSceneInMemory? _loadLoadEndTime : 0;
            }
        }

        internal void NotifySceneLoadingProgress(float progress)
        {
            if (IsSceneInMemory) throw new Exception("Scene load finished, Progress update not allowed");

            LoadingProgress = progress;

            OnLoadSceneStep();
        }

        internal void NotifySceneLoaded()
        {
            IsSceneInMemory = true;
            LoadingProgress = 1.0f;

            OnLoadSceneStep();
        }

        internal void NotifySceneLoadedAndActive()
        {
            if (IsSceneInMemory) return;

            IsSceneInMemory = true;
            IsSceneActivated = true;

            LoadingProgress = 1.0f;
            _loadLoadEndTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            _asyncOp = null;

            OnLoadSceneStep();
        }

        public override string ToString()
        {
            return string.Format("Scene \"{0}\"{1}{2} Id:{3}",
                SceneName,
                IsSceneLoadingAdditive? " Additive" : string.Empty,
                IsSceneLoadingAsync? " Async" : string.Empty,
                _id);
        }

        void OnLoadSceneStep()
        {
            if (_loadSceneStepCallback != null)
            {
                _loadSceneStepCallback(this);
            }
        }
    }

    /// <summary>
    ///     Central point for loading scenes either syncronous or asynchronous.
    ///     When loading scenes in an async way the SceneManager manages the loading process,
    ///     so you don't need to spawn Coroutines.
    ///
    ///     The SceneManager keeps track of:
    ///     - The name of the scene used to initialize the game (allows testing individual
    ///         scenes along the project depending on which scene you started from)
    ///     - The names of the scenes loaded in the game and the order in which they were loaded.
    ///       You'll need to put the inspector in debug mode to check this information
    ///
    ///     When loading a new scene you can be notified of the progress using these events:
    ///         SceneWillLoad:              raised when the SceneManager starts to load the scene
    ///
    ///         SceneDidLoad:               raised when the SceneManager finished loading the scene
    ///                                     and the elements of the scene are accesible
    ///
    ///         AsyncSceneLoadingProgress:  raised periodically to notify the load progress when
    ///                                     a scene is being loaded asynchronous
    ///
    ///     When loading an scene in an asynchronous way, the method will return a SceneLoadingArgs
    ///     instance, that raises events notifying only of the that scene loading progress or if the
    ///     scene finished loading. This serves as a simple way to attach a listener if you only are
    ///     interested on the events related to that scene. It is similar to the AsyncOperation
    ///     instance returned by the Application.LoadLevelAsync methods but they are not an
    ///     IEnumerator and thus they can't called with a Coroutine. No need to worry: the SceneManager
    ///     will take care of this plumbing.
    /// </summary>
    /// <remarks>
    ///     Monobehaviour Singleton: either if accessed by code or added to a scene this script will
    ///     always be located inside a GameObject marked as DontDestroyOnLoad so it lives during
    ///     all the execution of the app.
    ///     This MonoBehaviour should be the first thing executed when the app starts so remember
    ///     to update the ScriptExecutionOrder in the ProjectSettings
    /// </remarks>
    public class SceneManager : MonoBehaviourSingleton<SceneManager>
    {
        [Range(0.8f, 1f)]
        public float PercentTriggerSceneLoaded = 0.9f;


        #region State
        // We use fields so its values shows up in the inspector on debug mode
        private string _initialSceneName;
        private int _initialSceneIndex;
        List<string> _loadedScenes = new List<string>();

        SceneLoadingArgs CurrentlyLoadingSceneArgs{ get; set; }
        #endregion

        /// <summary>
        ///     Raised when the SceneManager starts to load the scene
        /// </summary>
        public event EventHandler<SceneLoadingArgs> SceneWillLoad;

        /// <summary>
        ///     Raised when the SceneManager finished loading the scene
        ///     and the elements of the scene are accesible
        /// </summary>
        public event EventHandler<SceneLoadingArgs> SceneDidLoad;

        /// <summary>
        ///     Raised periodically to notify the load progress when a scene is being
        ///     loaded asynchronous
        /// </summary>
        public event EventHandler<SceneLoadingArgs> AsyncSceneLoadingProgress;

        /// <summary>
        ///     Returns the name of the scene used to initializea the game
        /// </summary>
        /// <value>The initial name of the scene.</value>
        public string InitialSceneName { get {return _initialSceneName; } }

        /// <summary>
        ///     Returns the index of the scene used to initialize the game
        /// </summary>
        /// <value>The initial name of the scene.</value>
        public int InitialSceneIndex { get {return _initialSceneIndex; } }

        /// <summary>
        ///     Returns true if we are currently loading an scene.
        /// </summary>
        public bool IsLoadingScene { get { return CurrentlyLoadingSceneArgs != null; } }

        public System.Collections.ObjectModel.ReadOnlyCollection<string> LoadedScenes
        {
            get
            {
                return _loadedScenes.AsReadOnly();
            }

        }

        /// <summary>
        ///     Adds the contents of the scene with the given name to the current scene and the waits one frame
        ///     so all the newly loaded objects are actually accessible.
        ///     The SceneDidLoad event is raised after that frame has passed
        ///     This is equivalent to Application.LoadLevelAdditive() method
        /// </summary>
        /// <param name="sceneName">
        ///     The scene name which contents will be added to the current scene
        /// </param>
        /// <param name="finishedCallback">
        ///     This optional callback will be called when all the scenes have been loaded and one frame has passed,
        ///     which is the moment the contents of the scenes are actually accesible by code.
        /// </param>
        public void AddScene(string sceneName, Action<SceneLoadingArgs> finishedCallback = null)
        {
            var loadArgs = new SceneLoadingArgs(sceneName, true, false, finishedCallback);

            PrintLog("Start {0}", loadArgs);


            OnSceneWillLoad(loadArgs);

            Application.LoadLevelAdditive(loadArgs.SceneName);

            // Wait one frame so the GameObjects of the loaded scene are accesible, and then
            // this scene will be added to the list of loaded scenes
            StartCoroutine(WaitOneFrameUntilSceneIsAvailableCO(loadArgs));
        }

        /// <summary>
        ///     Adds the contents of a list of scenes to the current scene. After all the scenes are loaded
        ///     it waits one frame so all the newly loaded objects in all the scenes are accessible
        /// </summary>
        /// <param name="sceneNames">
        ///     A list with the names of the scenes to be added to the current scene. Scenes are added in
        ///     the order they appear in the list.
        /// </param>
        /// <param name="finishedCallback">
        ///     This optional callback will be called when all the scenes have been loaded and one frame has passed,
        ///     which is the moment the contents of the scenes are actually accesible by code.
        /// </param>
        public void AddScenes(IList<string> sceneNames, Action<SceneLoadingArgs> finishedCallback = null)
        {
            if (sceneNames.Count <= 0) return;

            int i = 0;
            SceneLoadingArgs loadArgs = null;

            do
            {
                loadArgs = new SceneLoadingArgs(sceneNames[i], true, false, finishedCallback);
                PrintLog("Start {0}", loadArgs);
                OnSceneWillLoad(loadArgs);
                Application.LoadLevelAdditive(loadArgs.SceneName);
                CurrentlyLoadingSceneArgs = null;
                i++;

            } while(i < sceneNames.Count);

            CurrentlyLoadingSceneArgs = loadArgs;

            // Wait one frame so the GameObjects of the loaded scene are accesible, and then
            // this scene will be added to the list of loaded scenes
            StartCoroutine(WaitOneFrameUntilSceneIsAvailableCO(loadArgs));
        }

        /// <summary>
        ///     Adds the contents of the scene with the given name to the current scene in an asynchronous process
        ///     so all the newly loaded objects are actually accessible. The scene is activated after it has
        ///     finished loading.
        ///     The SceneDidLoad event is raised after that frame has passed
        ///     This is equivalent to Application.LoadLevelAdditive() method
        /// </summary>
        /// <param name="sceneName">
        ///     The scene name which contents will be added to the current scene
        /// </param>
        /// <returns>
        ///     An SceneLoadingArgs instance that allows subscribing to events related
        ///     to this scene loading process
        /// </returns>
        /// <param name="finishedCallback">
        ///     This callback is called each frame until the scene is being loaded, so you can query the
        ///     loading progress, using SceneLoadingArgs.Progress. The callback is called one last
        ///     time when the scene has actually finished loading, has been loaded, and the elements in
        ///     the scene are accesible by code.
        ///     The property SceneLoadingArgs.IsLoaded can be queried to check in which of the
        ///     two states we are.
        /// </param>
        public void AddSceneAsync(string sceneName, Action<SceneLoadingArgs> finishedCallback = null)
        {
            AddSceneAsyncInternal(sceneName, true, finishedCallback);
        }

        /// <summary>
        ///     Adds the contents of the scene with the given name to the current scene in an asynchronous process
        ///     so all the newly loaded objects are actually accessible. The scene is NOT activated after it has
        ///     finished loading, you need to use the SceneLoadingArgs instance passed in the callback method
        ///     to manually activate the scene after it finished loading.
        ///
        ///     The SceneDidLoad event is raised after one frame has passed after the activation of the scene
        ///     This is equivalent to Application.LoadLevelAdditive() method and setting the
        ///     AsyncOperation.allowSceneActivation property returned by that method call to false.
        /// </summary>
        /// <param name="sceneName">
        ///     The scene name which contents will be added to the current scene
        /// </param>
        /// <returns>
        ///     An SceneLoadingArgs instance that allows subscribing to events related
        ///     to this scene loading process
        /// </returns>
        /// <param name="finishedCallback">
        ///     This callback is called each frame until the scene is being loaded, so you can query the
        ///     loading progress, using SceneLoadingArgs.Progress.
        ///     When the scene has finished loading, the callback is called once again to notify about this
        ///     and will wait until you manually call SceneLoadingArgs.ActivateScene() to actually load the
        ///     scene into Unity's scene tree
        ///     The callback is called one last time after the scene is activated and when the elements in
        ///     the scene are accesible by code.
        ///     The properties SceneLoadingArgs.IsLoaded and SceneLoadingArgs.IsActivated can be queried to check in which
        ///     state of the loading progress we are.
        /// </param>
        public void AddSceneAsyncWaitActivation(string sceneName, Action<SceneLoadingArgs> finishedCallback)
        {
            AddSceneAsyncInternal(sceneName, false, finishedCallback);
        }

        /// <summary>
        ///     Changes the current scene to the new one specified.
        ///     All GameObjects in the current scene will be deleted (except those marked as
        ///     DontDestroyOnLoad) and replaced by the contents of the new scene.
        /// </summary>
        /// <param name="sceneName">
        ///     The new scene to load
        /// </param>
        /// <param name="finishedCallback">
        ///     When the scene has been loaded and a frame has passed, this callback is called
        /// </param>
        public void ChangeSceneTo(string sceneName, Action<SceneLoadingArgs> finishedCallback = null)
        {
            var loadArgs = new SceneLoadingArgs(sceneName, false, false, finishedCallback);

            PrintLog("Start {0}", loadArgs);

            OnSceneWillLoad(loadArgs);

            Application.LoadLevel(loadArgs.SceneName);

            StartCoroutine(WaitOneFrameUntilSceneIsAvailableCO(loadArgs));
        }

        /// <summary>
        ///     Changes the current scene to the new one specified in an asynchronous way.
        ///     All GameObjects in the current scene will be deleted (except those marked as DontDestroyOnLoad) and
        ///     replaced by the contents of the new scene.
        /// </summary>
        /// <param name="sceneName">
        ///     The new scene to load.
        /// </param>
        /// <param name="activateOnLoad">
        ///     If set to <c>true</c> the GameObjects in the scene will activate automatically
        ///     when the scene is finished loading.
        /// </param>
        /// <param name="finishedCallback">
        ///     This callback is called each frame until the scene is loaded, so you can query the loading progress,
        ///     using SceneLoadingArgs.Progress. The callback is called one last time when the scene has actually
        ///     finished loading and the elements in the scene are accesible.
        ///     Property SceneLoadingArgs.IsLoaded can be queried to check in which of the two cases we are.
        /// </param>
        public void ChangeSceneToAsync(string sceneName, bool activateOnLoad, Action<SceneLoadingArgs> finishedCallback)
        {
            var loadArgs = new SceneLoadingArgs(sceneName, false, true, finishedCallback);

            PrintLog("Start {0}", loadArgs);

            OnSceneWillLoad(loadArgs);

            StartCoroutine(LoadLevelAsyncCO(activateOnLoad));
        }

        /// <summary>
        ///     Queries if the given scene is already loaded
        /// </summary>
        /// <param name="sceneName">
        ///     Name of the scene to check
        /// </param>
        /// <returns>
        ///     <c>true</c> if the given scene is loaded otherwise, <c>false</c>.
        /// </returns>
        public bool IsSceneLoaded(string sceneName)
        {
            return LoadedScenes.Contains(sceneName);
        }

        #region MonobehaviourSingleton overrides

        protected override void SingletonAwakened()
        {
            _initialSceneName = Application.loadedLevelName;
            _initialSceneIndex = Application.loadedLevel;
            _loadedScenes.Add(_initialSceneName);

            PrintLog("Game started with scene {0}", _initialSceneName);
        }
        #endregion

        #region Helpers


        private void AddSceneAsyncInternal(string sceneName, bool activateOnLoad, Action<SceneLoadingArgs> finishedCallback)
        {
            var loadArgs = new SceneLoadingArgs(sceneName, true, true, finishedCallback);

            PrintLog("Start {0}", loadArgs);

            OnSceneWillLoad(loadArgs);

            StartCoroutine(LoadLevelAsyncCO(activateOnLoad));
        }


        /// <summary>
        ///     Used by a coroutine to load a scene in an async way and to issue progress events
        /// </summary>
        IEnumerator LoadLevelAsyncCO(bool activateOnLoad)
        {
            var asyncOp = CurrentlyLoadingSceneArgs.IsSceneLoadingAdditive
                    ? Application.LoadLevelAdditiveAsync(CurrentlyLoadingSceneArgs.SceneName)
                    : Application.LoadLevelAsync(CurrentlyLoadingSceneArgs.SceneName);

            asyncOp.allowSceneActivation = activateOnLoad;
            CurrentlyLoadingSceneArgs.SaveAsyncOperationReference(asyncOp);

            // As of Unity 5.3 loads scenes asynchronous while defering the activation of the scene.
            // If AsyncOperation.allowSceneActivation is false Unity loads the scene but
            // does not activate it. That means the scene is loaded in memory but no scene
            // change will take place until you assign that property a value of true.
            // The trick is that Unity considers an scene fully loaded
            // when the scene is loaded AND activated, so until you activate the scene
            // asyncOp.isDone is false, and asyncOp.percent is not 1, so if you set 
            // asyncOp.allowSceneActivation  to false, asyncOp.isDone, will never be true until you set
            // it by code.
            // This prevent using a loop yielding the async operation coroutine and checking for the
            // scene to be loaded, as it would never exit the loop.
            // The common hack to solve this is check that the progress is around
            // 90%, as that is considered the moment that the scene is loaded but
            // just not activated. I've seen this recomended by Unity devs, 
            // however looks more like a magic number based on common experience.
            while(!IsSceneLoaded(asyncOp))
            {
                CurrentlyLoadingSceneArgs.NotifySceneLoadingProgress(asyncOp.progress);

                yield return null;
            }

            // Notify that the scene was loaded but is not active
            if(!asyncOp.allowSceneActivation) CurrentlyLoadingSceneArgs.NotifySceneLoaded();

            // Wait until we activate the scene
            while (!asyncOp.allowSceneActivation)
            {
                yield return null;
            }

            OnSceneDidLoad();
        }

        bool IsSceneLoaded(AsyncOperation asyncOp)
        {
            if(!asyncOp.allowSceneActivation)
            {
                return asyncOp.progress >= this.PercentTriggerSceneLoaded;;
            }
            else
            {
                return asyncOp.isDone;
            }

        }
        /// <summary>
        ///     Waits a frame after the scene is loaded so the GameObjects 
        ///     it contains are accessible by code.
        /// </summary>
        IEnumerator WaitOneFrameUntilSceneIsAvailableCO(SceneLoadingArgs eventArgs)
        {
            yield return null;

            OnSceneDidLoad();
        }

        void OnSceneDidLoad()
        {
            // If is not an additive operation, clean the list of loaded scenes
            if (!CurrentlyLoadingSceneArgs.IsSceneLoadingAdditive)
            {
                _loadedScenes.Clear();
            }

            _loadedScenes.Add(CurrentlyLoadingSceneArgs.SceneName);

            // If CurrentlyLoadingSceneArgs is not null means that we  we are in the process
            // of loading a scene. We need to set it to null before raising any event / callback
            // notifing that the scene was loaded to allow loading a new scene inside those events.
            var justLoadedSceneArgs = CurrentlyLoadingSceneArgs;
            CurrentlyLoadingSceneArgs = null;

            justLoadedSceneArgs.NotifySceneLoadedAndActive();

            if (SceneDidLoad != null) SceneDidLoad(this, justLoadedSceneArgs);

            PrintLog("Loaded {0}", justLoadedSceneArgs);
        }

        void OnSceneWillLoad(SceneLoadingArgs args)
        {
            // Unity does not allow loading several async scenes at the same time:
            // the second async loaded scene would fail silently so at least we 
            // log the problem

            if (IsLoadingScene)
            {
                Debug.Log (string.Format ("Could not load {0}: Already loading {1}",
                    args,
                    CurrentlyLoadingSceneArgs));
                
                return;
            }

            CurrentlyLoadingSceneArgs = args;

            if (SceneWillLoad != null) SceneWillLoad(this, CurrentlyLoadingSceneArgs);
        }

        #endregion

    }
}