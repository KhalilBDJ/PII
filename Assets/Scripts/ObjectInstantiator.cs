using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


[RequireComponent(typeof(ARTrackedImageManager))]
public class ObjectInstantiator : MonoBehaviour
{
    private ARTrackedImageManager _trackedImageManager;

    // Prefab à instancier
    public GameObject prefabToSpawn;

    // Dictionnaire pour suivre les instances des prefabs
    private readonly Dictionary<string, GameObject> _instantiatedPrefabs = new Dictionary<string, GameObject>();

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    private void OnEnable()
    {
        _trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        _trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Pour chaque image nouvellement détectée
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            // Instancie un nouveau prefab s'il n'existe pas encore
            if (!_instantiatedPrefabs.ContainsKey(trackedImage.referenceImage.name))
            {
                GameObject newPrefab = Instantiate(prefabToSpawn, trackedImage.transform);
                _instantiatedPrefabs.Add(trackedImage.referenceImage.name, newPrefab);
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
            prefab.transform.position = trackedImage.transform.position;
            prefab.transform.rotation = trackedImage.transform.rotation;

            // Mise à jour de la taille du prefab pour correspondre à celle de l'image
            Vector3 newScale = new Vector3(trackedImage.size.x * 10, 0.1f, trackedImage.size.y * 10);
            prefab.transform.localScale = newScale;
        }
    }
}
