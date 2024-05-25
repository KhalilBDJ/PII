using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Image = UnityEngine.UIElements.Image;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ObjectInstantiator : MonoBehaviour
{
    private ARTrackedImageManager _trackedImageManager;
    private MutableRuntimeReferenceImageLibrary _mutableLibrary;
    private ARAnchorManager _anchorManager;

    public TMP_Text text;
    public GameObject prefabToSpawn;

    private readonly Dictionary<string, GameObject> _instantiatedPrefabs = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, ARAnchor> _anchors = new Dictionary<string, ARAnchor>();

    private const string apiUrl = "http://192.168.1.100:8080/api/images/";

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        _anchorManager = GetComponent<ARAnchorManager>();
    }

    private void OnEnable()
    {
        StartCoroutine(FetchAndLoadImages());
        _trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        _trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private IEnumerator FetchAndLoadImages()
    {
        UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        yield return request.SendWebRequest();
        SetUIText("Fetching");

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error fetching images: " + request.error);
            SetUIText("Error fetching images: " + request.error);
        }
        else
        {
            List<ImageObject> imageObjects = JsonUtility.FromJson<ImageObjectList>("{\"images\":" + request.downloadHandler.text + "}").images;

            _mutableLibrary = _trackedImageManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
            SetUIText("Success");
            Debug.Log("C'est un succès");

            if (_mutableLibrary == null)
            {
                Debug.LogError("Failed to get MutableRuntimeReferenceImageLibrary.");
                SetUIText("Failed to get MutableRuntimeReferenceImageLibrary.");
                yield break;
            }

            Dictionary<Texture2D, XRInfo> reconstructedImages = new Dictionary<Texture2D, XRInfo>();
            foreach (ImageObject imageObject in imageObjects)
            {
                byte[] imageBytes = System.Convert.FromBase64String(imageObject.image);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageBytes);
                texture.hideFlags = HideFlags.DontUnloadUnusedAsset;

                // Verify the texture dimensions
                if (texture.width == 0 || texture.height == 0)
                {
                    Debug.LogError($"Invalid image dimensions for {imageObject.name}");
                    continue;
                }

                XRInfo xrInfo = new XRInfo
                {
                    name = imageObject.name,
                    specifySize = true,
                    size = new Vector2(0.5f, 0.5f) // Exemple de taille, à ajuster selon les besoins
                };

                reconstructedImages.Add(texture, xrInfo);
            }

            AddImagesToLibrary(_mutableLibrary, reconstructedImages);
        }
    }

    private void AddImagesToLibrary(MutableRuntimeReferenceImageLibrary mutableLibrary, Dictionary<Texture2D, XRInfo> reconstructedImages)
    {
        foreach (KeyValuePair<Texture2D, XRInfo> entry in reconstructedImages)
        {
            try
            {
                Texture2D newImageTexture = entry.Key;
                string newImageName = entry.Value.name;
                float? newImageWidthInMeters = entry.Value.specifySize ? entry.Value.size.x : (float?)null;

                // Log texture info
                Debug.Log($"Adding image {newImageName} with size {newImageTexture.width}x{newImageTexture.height} and width {newImageWidthInMeters} meters");

                AddReferenceImageJobState jobState = mutableLibrary.ScheduleAddImageWithValidationJob(
                    newImageTexture,
                    newImageName,
                    newImageWidthInMeters
                );

                JobHandle jobHandle = jobState.jobHandle;
                jobHandle.Complete();

                if (jobState.status == AddReferenceImageJobStatus.Success)
                {
                    Debug.Log($"Image {newImageName} added to library successfully.");
                }
                else
                {
                    Debug.LogWarning($"Failed to add image {newImageName} to library. {jobState.status}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to add image {entry.Value.name} to library. {e}");
            }
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            SetUIText("Image Detected");
            Debug.Log("Image détectée");

            if (!_instantiatedPrefabs.ContainsKey(trackedImage.referenceImage.name))
            {
                ARAnchor anchor = _anchorManager.AddAnchor(new Pose(trackedImage.transform.position, Quaternion.identity));
                if (anchor != null)
                {
                    GameObject newPrefab = Instantiate(prefabToSpawn, anchor.transform);
                    newPrefab.SetActive(true);
                    _instantiatedPrefabs.Add(trackedImage.referenceImage.name, newPrefab);
                    _anchors.Add(trackedImage.referenceImage.name, anchor);
                    Debug.Log(trackedImage.referenceImage.name);
                }
            }

            UpdatePrefabTransform(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            UpdatePrefabTransform(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.removed)
        {
            if (_instantiatedPrefabs.ContainsKey(trackedImage.referenceImage.name))
            {
                Destroy(_instantiatedPrefabs[trackedImage.referenceImage.name]);
                _instantiatedPrefabs.Remove(trackedImage.referenceImage.name);
            }

            if (_anchors.ContainsKey(trackedImage.referenceImage.name))
            {
                Destroy(_anchors[trackedImage.referenceImage.name]);
                _anchors.Remove(trackedImage.referenceImage.name);
            }
        }
    }

    private void UpdatePrefabTransform(ARTrackedImage trackedImage)
    {
        if (_instantiatedPrefabs.TryGetValue(trackedImage.referenceImage.name, out GameObject prefab))
        {
            if (_anchors.TryGetValue(trackedImage.referenceImage.name, out ARAnchor anchor))
            {
                anchor.transform.position = trackedImage.transform.position;
                anchor.transform.rotation = trackedImage.transform.rotation;
            }

            prefab.transform.localPosition = Vector3.zero;
            prefab.transform.localRotation = Quaternion.identity;

            // Apply the tracked image's rotation to the prefab and adjust its orientation
            Quaternion imageRotation = trackedImage.transform.rotation;
            Quaternion correctionRotation = Quaternion.Euler(90, 0, 0); // Adjust this as needed
            prefab.transform.rotation = imageRotation * correctionRotation;

            Vector3 newScale = new Vector3(trackedImage.size.x, trackedImage.size.y, 0.1f);
            prefab.transform.localScale = newScale;
        }
    }

    private void SetUIText(string message)
    {
        text.text = message;
    }
}

[System.Serializable]
public class ImageObject
{
    public string name;
    public string image;
    public string text;
}

[System.Serializable]
public class ImageObjectList
{
    public List<ImageObject> images;
}

public class XRInfo
{
    public string name;
    public bool specifySize;
    public Vector2 size;
}

public class AcceptAllCertificatesSignedHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}
