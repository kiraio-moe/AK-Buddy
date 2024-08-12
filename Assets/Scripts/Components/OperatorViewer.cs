using System;
using System.Collections.Generic;
using Arknights.Core;
using Arknights.Utils;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Arknights.Components
{
    [AddComponentMenu("Arknights/Components/Operator Viewer")]
    public class OperatorViewer : MonoBehaviour
    {
        [Header("Spine")]
        [SerializeField]
        string m_DefaultAnimation = "Relax";

        [SerializeField]
        string m_TouchAnimation = "Interact";

        [SerializeField]
        string m_MoveAnimation = "Move";

        [SerializeField]
        string m_SitAnimation = "Sit";

        [SerializeField]
        string m_SleepAnimation = "Sleep";

        [SerializeField]
        float m_SkeletonScale = 0.25f;

        [Space]
        [SerializeField]
        AudioSource m_OperatorAudioSource;

        [SerializeField]
        OperatorData operatorData = new();

        SkeletonAnimation skeletonAnimation;
        bool allowInteraction = true;
        int touchVoiceIndex;
        List<AudioClip> touchVoices = new();

        public delegate void OnOperatorCreatedHandler();
        public event OnOperatorCreatedHandler OnOperatorCreated;

        InputManager inputManager;
        readonly float dragSmoothTime = .1f;
        Vector2 dragObjectVelocity,
            dragObjectOffset;
        bool isDragged;

        public OperatorData OperatorData
        {
            get => operatorData;
            set => operatorData = value;
        }
        public AudioSource OperatorAudioSource
        {
            get => m_OperatorAudioSource;
            set => m_OperatorAudioSource = value;
        }
        public bool AllowInteraction
        {
            get => allowInteraction;
            set => allowInteraction = value;
        }
        public int TouchVoiceIndex
        {
            get => touchVoiceIndex;
            set => touchVoiceIndex = value;
        }
        public List<AudioClip> TouchVoices
        {
            get => touchVoices;
            set => touchVoices = value;
        }
        public SkeletonAnimation SkeletonAnimation
        {
            get => skeletonAnimation;
            set => skeletonAnimation = value;
        }

        void Awake()
        {
            inputManager = FindObjectsByType<InputManager>(FindObjectsSortMode.None)[0];
        }

        void OnEnable()
        {
            inputManager.Click.performed += Interact;
            inputManager.ClickHold.performed += DragOperator;
            OnOperatorCreated += Wander;
        }

        void OnDisable()
        {
            inputManager.Click.performed -= Interact;
            inputManager.ClickHold.performed -= DragOperator;
            OnOperatorCreated -= Wander;
        }

        void OnDestroy()
        {
            inputManager.Click.performed -= Interact;
            inputManager.ClickHold.performed -= DragOperator;
            OnOperatorCreated -= Wander;
        }

        public async UniTask<SkeletonAnimation> CreateOperator()
        {
            try
            {
                skeletonAnimation = await SpineHelper.InstantiateSpine(
                    OperatorData.SkeletonPath,
                    OperatorData.AtlasPath,
                    OperatorData.TexturePath,
                    gameObject,
                    Shader.Find("Spine/Skeleton"),
                    spineScale: m_SkeletonScale,
                    loop: true,
                    defaultAnimation: m_DefaultAnimation
                );
                AddMeshCollider();
                await CacheVoices();
                OnOperatorCreated?.Invoke();
                return skeletonAnimation;
            }
            catch
            {
                Debug.LogError("Error creating Operator!");
                return null;
            }
        }

        void AddMeshCollider()
        {
            MeshCollider meshCollider =
                gameObject.AddComponent(typeof(MeshCollider)) as MeshCollider;
            if (TryGetComponent(out MeshFilter meshFilter))
                meshCollider.sharedMesh = meshFilter.sharedMesh;
        }

        async UniTask CacheVoices()
        {
            foreach (string voice in OperatorData.VoicesPath)
                TouchVoices.Add(await WebRequestHelper.GetAudioClip(voice));
        }

        async void DragOperator(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (hit.collider.TryGetComponent(out OperatorViewer viewer))
                    {
                        // Stop wandering for this specific character
                        viewer.AllowInteraction = false;

                        // Set the animation to walking only for the dragged character
                        if (
                            viewer.skeletonAnimation.AnimationState.GetCurrent(0).Animation.Name
                            != viewer.m_MoveAnimation
                        )
                        {
                            viewer.skeletonAnimation.AnimationState.SetAnimation(
                                0,
                                viewer.m_MoveAnimation,
                                true
                            );
                        }

                        await viewer.DragUpdate(hit.collider.gameObject);
                    }
                }
            }
        }

        async UniTask DragUpdate(GameObject clickedObject)
        {
            float initialDistance = Vector3.Distance(
                clickedObject.transform.position,
                Camera.main.transform.position
            );
            Ray initialRay = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Vector3 initialPoint = initialRay.GetPoint(initialDistance);
            dragObjectOffset = clickedObject.transform.position - initialPoint;

            Vector2 previousPosition = clickedObject.transform.position;

            while (inputManager.ClickHold.ReadValue<float>() != 0)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                Vector2 targetPoint = (Vector2)ray.GetPoint(initialDistance) + dragObjectOffset;
                clickedObject.transform.position = Vector2.SmoothDamp(
                    clickedObject.transform.position,
                    targetPoint,
                    ref dragObjectVelocity,
                    dragSmoothTime
                );

                // Calculate the movement direction
                Vector2 currentPosition = clickedObject.transform.position;
                Vector2 direction = currentPosition - previousPosition;

                // Update the scale based on the direction of movement
                if (direction.x > 0)
                {
                    clickedObject.transform.localScale = new Vector2(
                        1,
                        clickedObject.transform.localScale.y
                    );
                }
                else if (direction.x < 0)
                {
                    clickedObject.transform.localScale = new Vector2(
                        -1,
                        clickedObject.transform.localScale.y
                    );
                }

                previousPosition = currentPosition;

                AllowInteraction = false;
                isDragged = true;
                await UniTask.Yield();
            }

            isDragged = false;

            // Reset animation to idle after dragging
            skeletonAnimation.AnimationState.SetAnimation(0, m_DefaultAnimation, true);

            // Wait for a random time before allowing interaction and triggering wander again
            float waitTime = Random.Range(3f, 5f); // Customize these values as needed
            await UniTask.Delay(TimeSpan.FromSeconds(waitTime));

            // Re-enable interaction and let the wander event trigger naturally
            AllowInteraction = true;

            // The wander behavior will be triggered by your event system once AllowInteraction is true
        }

        void Interact(InputAction.CallbackContext ctx)
        {
            if (ctx.performed && AllowInteraction)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (hit.collider.TryGetComponent(out OperatorViewer viewer))
                    {
                        if (viewer == this)
                        {
                            AllowInteraction = false;
                            Spine.Animation touchAnimation = skeletonAnimation
                                .skeletonDataAsset.GetAnimationStateData()
                                .SkeletonData.FindAnimation(m_TouchAnimation);

                            if (touchAnimation != null)
                            {
                                if (TouchVoices != null && TouchVoices.Count > 0)
                                {
                                    OperatorAudioSource.clip = TouchVoices[TouchVoiceIndex];
                                    OperatorAudioSource.Play();
                                    TouchVoiceIndex = (TouchVoiceIndex + 1) % TouchVoices.Count;
                                }

                                skeletonAnimation
                                    .AnimationState.SetAnimation(0, m_TouchAnimation, false)
                                    .MixDuration = 0.5f;
                                skeletonAnimation.AnimationState.GetCurrent(0).Complete +=
                                    async _ =>
                                    {
                                        if (isDragged)
                                            skeletonAnimation.AnimationState.AddAnimation(
                                                0,
                                                m_MoveAnimation,
                                                true,
                                                0
                                            );
                                        else
                                            skeletonAnimation.AnimationState.AddAnimation(
                                                0,
                                                m_DefaultAnimation,
                                                true,
                                                0
                                            );
                                        if (OperatorAudioSource != null)
                                        {
                                            await UniTask.WaitUntil(
                                                () =>
                                                    OperatorAudioSource != null
                                                    && !OperatorAudioSource.isPlaying
                                            );
                                        }
                                        AllowInteraction = true;
                                    };
                            }
                        }
                    }
                }
            }
        }

        async void Wander()
        {
            if (this != null || gameObject != null)
                await Wander(gameObject, 1f, 1f, 3f, 4f, 2f, 5f);
        }

        async UniTask Wander(
            GameObject character,
            float minSpeed,
            float maxSpeed,
            float minWaitTime,
            float maxWaitTime,
            float minDistance,
            float maxDistance,
            float sitChance = 0.2f, // Chance to sit
            float sleepChance = 0.19f // Chance to sleep
        )
        {
            while (true) // Infinite loop to keep the character wandering
            {
                if (this == null || character == null || SkeletonAnimation == null)
                    break;

                // Wait if the character is being dragged or interaction is not allowed
                while (isDragged || !AllowInteraction)
                {
                    if (this == null || character == null || SkeletonAnimation == null)
                        break;

                    await UniTask.Yield();
                }

                if (this == null || character == null || SkeletonAnimation == null)
                    break;

                // Idle for a random time before starting to wander
                float initialWaitTime = Random.Range(minWaitTime, maxWaitTime);
                await UniTask.Delay(TimeSpan.FromSeconds(initialWaitTime));

                if (this == null || character == null || SkeletonAnimation == null)
                    break;

                // If the character is idle, set the wandering animation
                if (
                    SkeletonAnimation.AnimationState.GetCurrent(0).Animation.Name != m_MoveAnimation
                )
                {
                    SkeletonAnimation.AnimationState.SetAnimation(0, m_MoveAnimation, true);
                }

                // Randomly determine the direction (-1 for left, 1 for right)
                float direction = Random.value < 0.5f ? -1f : 1f;

                // Randomly determine the distance to move and the speed
                float distance = Random.Range(minDistance, maxDistance);
                float speed = Random.Range(minSpeed, maxSpeed);

                // Calculate the target position
                Vector3 targetPosition =
                    character.transform.position + new Vector3(direction * distance, 0, 0);

                // Check if the target position is within screen bounds
                Vector3 screenPoint = Camera.main.WorldToViewportPoint(targetPosition);
                bool isWithinScreen = screenPoint.x > 0 && screenPoint.x < 1;

                if (isWithinScreen)
                {
                    // Start moving towards the target position
                    while (
                        character != null
                        && Vector3.Distance(character.transform.position, targetPosition) > 0.1f
                        && !isDragged
                        && AllowInteraction
                    )
                    {
                        if (this == null || character == null || SkeletonAnimation == null)
                            break;

                        character.transform.position = Vector3.MoveTowards(
                            character.transform.position,
                            targetPosition,
                            speed * Time.deltaTime
                        );

                        // Update the scale based on the direction
                        character.transform.localScale = new Vector3(
                            direction,
                            character.transform.localScale.y,
                            character.transform.localScale.z
                        );

                        await UniTask.Yield();

                        // Pause wandering if dragged or interaction is disabled
                        if (isDragged || !AllowInteraction)
                        {
                            break;
                        }
                    }
                }

                if (this == null || character == null || SkeletonAnimation == null)
                    break;

                // Random chance to sit or sleep with smooth transitions
                float randomValue = Random.value;
                if (randomValue < sleepChance)
                {
                    // Smooth transition to sleep animation
                    SkeletonAnimation
                        .AnimationState.SetAnimation(0, m_SleepAnimation, false)
                        .MixDuration = 0.5f;
                    await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(5f, 10f))); // Sleep duration
                }
                else if (randomValue < sitChance + sleepChance)
                {
                    // Smooth transition to sit animation
                    SkeletonAnimation
                        .AnimationState.SetAnimation(0, m_SitAnimation, false)
                        .MixDuration = 0.5f;
                    await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(3f, 5f))); // Sit duration
                }

                // Randomly determine the wait time before wandering again
                if (SkeletonAnimation != null)
                    SkeletonAnimation
                        .AnimationState.SetAnimation(0, m_DefaultAnimation, true)
                        .MixDuration = 0.5f;
                float waitTime = Random.Range(minWaitTime, maxWaitTime);
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime));
            }
        }
    }
}
