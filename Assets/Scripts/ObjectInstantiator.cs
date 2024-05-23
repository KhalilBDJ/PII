using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ObjectInstantiator : MonoBehaviour
{
    private ARTrackedImageManager _trackedImageManager;
    private MutableRuntimeReferenceImageLibrary _mutableLibrary;
    private List<string> _existingImageNames = new List<string>();

    public TMP_Text text;

    public RawImage imageTest;
    // Prefab à instancier
    public GameObject prefabToSpawn;

    // Dictionnaire pour suivre les instances des prefabs
    private readonly Dictionary<string, GameObject> _instantiatedPrefabs = new Dictionary<string, GameObject>();

    // URL de l'API Spring Boot pour récupérer les images
    private const string apiUrl = "http://localhost:8080/api/images/";

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
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

            // Clear the existing library
            ClearExistingLibrary();

            // Add new images to the AR library
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
        imageTest.texture = texture;
        _mutableLibrary.ScheduleAddImageWithValidationJob(texture, imageObject.name, 0.5f);
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Pour chaque image nouvellement détectée
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            if (_mutableLibrary.count>0)
            {
                // Instancie un nouveau prefab s'il n'existe pas encore
                if (!_instantiatedPrefabs.ContainsKey(trackedImage.referenceImage.name))
                {
                    GameObject newPrefab = Instantiate(prefabToSpawn, trackedImage.transform);
                    newPrefab.SetActive(true);
                    _instantiatedPrefabs.Add(trackedImage.referenceImage.name, newPrefab);
                    text.text = trackedImage.referenceImage.name;
                }
            }
          

            // Ajuste la taille du prefab pour qu'il corresponde à celle de l'image détectée
            UpdatePrefabTransform(trackedImage);
        }

        // Pour chaque image mise à jour
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            UpdatePrefabTransform(trackedImage);
        }

        // Gère les images supprimées (désactive les prefabs associés)
        foreach (ARTrackedImage trackedImage in eventArgs.removed)
        {
            if (_instantiatedPrefabs.ContainsKey(trackedImage.referenceImage.name))
            {
                _instantiatedPrefabs[trackedImage.referenceImage.name].SetActive(false);
            }
        }
    }

    // Met à jour la taille et la position du prefab en fonction de l'image AR détectée
    private void UpdatePrefabTransform(ARTrackedImage trackedImage)
    {
        if (_instantiatedPrefabs.TryGetValue(trackedImage.referenceImage.name, out GameObject prefab))
        {
            prefab.transform.localPosition = new Vector3(trackedImage.size.x / 2, 0, 0);

            prefab.transform.rotation = Quaternion.Euler(trackedImage.transform.rotation.eulerAngles);

            // Mise à jour de la taille du prefab pour correspondre à celle de l'image
            Vector3 newScale = new Vector3(trackedImage.size.x, trackedImage.size.y, 0.1f);
            prefab.transform.localScale = newScale;
        }
    }
    
    
}

[System.Serializable]
public class ImageObject
{
    public string name;
    public string image;  // Base64 encoded string
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
        return true; // Certificat toujours valide
    }
}

