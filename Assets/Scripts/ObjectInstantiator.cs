using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using Unity.Collections;
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
    private List<string> _existingImageNames = new List<string>();

    public TMP_Text text;
    public RawImage imageTest;
    public GameObject prefabToSpawn;

    private readonly Dictionary<string, GameObject> _instantiatedPrefabs = new Dictionary<string, GameObject>();

    private const string apiUrl = "http://172.20.10.3:8080/api/images/";

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    private void Update()
    {
        //Debug.Log(_trackedImageManager.referenceLibrary.count);
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
        request.certificateHandler = new AcceptAllCertificatesSignedHandler();
        yield return request.SendWebRequest();
        text.text = "Fetching";

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error fetching images: " + request.error);
            text.text = "Error fetching images: " + request.error;
        }
        else
        {
            List<ImageObject> imageObjects = JsonUtility.FromJson<ImageObjectList>("{\"images\":" + request.downloadHandler.text + "}").images;

            _mutableLibrary = _trackedImageManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
            text.text = "Success";

            if (_mutableLibrary == null)
            {
                Debug.LogError("Failed to get MutableRuntimeReferenceImageLibrary.");
                text.text = "Failed to get MutableRuntimeReferenceImageLibrary.";
                yield break;
            }

            ClearExistingLibrary();

            foreach (ImageObject imageObject in imageObjects)
            {
                if (!_existingImageNames.Contains(imageObject.name))
                {
                    AddImageToLibrary(imageObject);
                    _existingImageNames.Add(imageObject.name);
                }
            }
        }
    }

    private void ClearExistingLibrary()
    {
        _mutableLibrary = _trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
        _trackedImageManager.referenceLibrary = _mutableLibrary;
        _existingImageNames.Clear();
    }

    private void AddImageToLibrary(ImageObject imageObject)
    {
        byte[] imageBytes = System.Convert.FromBase64String(imageObject.image);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes);
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;

        // Différer la mise à jour du RawImage pour éviter l'erreur de boucle de reconstruction graphique
        StartCoroutine(UpdateRawImage(texture));

        // Définir une taille par défaut en mètres
        float defaultSize = 0.5f; // Par exemple, 0.5 mètres

        // Ajouter l'image à la bibliothèque mutable avec la taille par défaut
        _mutableLibrary.ScheduleAddImageWithValidationJob(texture, imageObject.name, defaultSize);
        Debug.Log("Nombre d'images : " + _trackedImageManager.referenceLibrary.count);
    }

    private IEnumerator UpdateRawImage(Texture2D texture)
    {
        yield return new WaitForEndOfFrame();
        imageTest.texture = texture;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            text.text = "Image Detected";

            if (_mutableLibrary.count > 0)
            {
                if (!_instantiatedPrefabs.ContainsKey(trackedImage.referenceImage.name))
                {
                    GameObject newPrefab = Instantiate(prefabToSpawn, trackedImage.transform);
                    newPrefab.SetActive(true);
                    _instantiatedPrefabs.Add(trackedImage.referenceImage.name, newPrefab);
                    //text.text = trackedImage.referenceImage.name;
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
                _instantiatedPrefabs[trackedImage.referenceImage.name].SetActive(false);
            }
        }
    }

    private void UpdatePrefabTransform(ARTrackedImage trackedImage)
    {
        if (_instantiatedPrefabs.TryGetValue(trackedImage.referenceImage.name, out GameObject prefab))
        {
            prefab.transform.localPosition = new Vector3(trackedImage.size.x / 2, 0, 0);
            Vector3 imageRot = trackedImage.transform.rotation.eulerAngles;
            prefab.transform.rotation = Quaternion.Euler(new Vector3(imageRot.x + 90, imageRot.y, imageRot.z));
            Vector3 newScale = new Vector3(trackedImage.size.x, trackedImage.size.y, 0.1f);
            prefab.transform.localScale = newScale;
        }
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

public class AcceptAllCertificatesSignedHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}
