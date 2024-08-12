using System;
using System.Collections.Generic;
using System.IO;
using Arknights.Components;
using Cysharp.Threading.Tasks;
using Kirurobo;
using UnityEngine;

namespace Arknights.Core
{
    [AddComponentMenu("Arknights/Core/Main Control")]
    public class MainControl : MonoBehaviour
    {
        [SerializeField]
        OperatorViewer m_OperatorPrefab;

        UniWindowController windowController;

        void Awake()
        {
            windowController = FindObjectsByType<UniWindowController>(FindObjectsSortMode.None)[0];
            windowController.allowDropFiles = true;
            windowController.currentCamera = Camera.main;
#if !UNITY_EDITOR
            windowController.isTransparent = true;
            windowController.isTopmost = true;
            windowController.shouldFitMonitor = true;
#endif
        }

        void OnEnable()
        {
            windowController.OnDropFiles += OnFilesDrop;
        }

        void OnDisable()
        {
            windowController.OnDropFiles -= OnFilesDrop;
        }

        async void OnFilesDrop(string[] files)
        {
            string atlas = "",
                texture = "",
                skeleton = "";
            List<string> voices = new();

            foreach (string file in files)
            {
                switch (Path.GetExtension(file).ToLower())
                {
                    case ".atlas":
                        atlas = file;
                        break;
                    case ".png":
                        texture = file;
                        break;
                    case ".skel":
                        skeleton = file;
                        break;
                    case ".mp3":
                    case ".ogg":
                    case ".wav":
                        voices.Add(file);
                        break;
                    default:
                        Debug.LogError($"Unsupported file type: {Path.GetExtension(file)}");
                        return;
                }
            }

            OperatorViewer akOperator = Instantiate(m_OperatorPrefab);
            akOperator.OperatorData.AtlasPath = atlas;
            akOperator.OperatorData.SkeletonPath = skeleton;
            akOperator.OperatorData.TexturePath = texture;
            akOperator.OperatorData.VoicesPath = voices;
            await akOperator.CreateOperator();
        }
    }
}
