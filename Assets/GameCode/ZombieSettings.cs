using UnityEngine;

public class ZombieSettings : MonoBehaviour
{
    public int HumanCount = 1000;
    public int ZombieCount = 10;

    public float HumanSpeed = 10.0f;
    public float ZombieSpeed = 12.5f;
    
    public float InfectionDistance = 1f;

    public Rect Playfield = new Rect { x = -30.0f, y = -30.0f, width = 60.0f, height = 60.0f };

    public static ZombieSettings Instance { get; private set; }
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;
        }
    }


}