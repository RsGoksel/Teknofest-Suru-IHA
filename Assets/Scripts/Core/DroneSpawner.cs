using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// TEKNOFEST Sürü İHA Yarışması - Ana Kontrol Sistemi
/// Takım: COMBINE - Hibrit Sürü Zekası Projesi
/// </summary>
public class DroneSpawner : MonoBehaviour
{
    [Header("TEKNOFEST ŞARTNAME PARAMETRELERİ")]
    [Space(10)]
    [Tooltip("Z - Uçuş İrtifası (metre)")]
    public float ucusIrtifasi = 10f;
    
    [Tooltip("T - Formasyon Koruma Süresi (saniye)")]
    public float formasyonKorumaSuresi = 30f;
    
    [Tooltip("X - Ajanlar Arası Mesafe (metre)")]
    public float ajanlarArasiMesafe = 5f;
    
    [Space(5)]
    [Header("YARIŞMA SENARYOLARI")]
    public bool arrowFormationAktif = true;
    public bool vFormationAktif = true;
    public bool lineFormationAktif = true;
    public bool verticalFormationAktif = true;
    
    [Header("TEKNOFEST RUNTIME KONTROLLERI")]
    [Tooltip("Parametreleri yarışma günü güncellemek için")]
    public bool parametreleriGuncelle = false;
    
    [Header("Spawn Settings")]
    public GameObject dronePrefab;
    public int numberOfDrones = 10;
    public float spacingX = 3f;
    public float spacingZ = 3f;
    public float groundHeight = 1f;
    
    [Header("Flight Settings")]
    public float takeoffHeight = 5f;
    public float formationHeight = 10f;
    public float thrustForce = 15f;
    public float moveForce = 5f;
    
    [Header("Smart Communication Settings")]
    public float communicationRange = 15;
    public float safetyDistance = 20f;
    public float avoidanceRadius = 5f;
    public float stagingRadius = 4f;
    public float decisionSpeed = 0.01f;
    public float movementSpeed = 3.0f;
    public float formationSpeed = 0.4f;
    
    // System State
    private List<GameObject> spawnedDrones = new List<GameObject>();
    private List<SmartDronePhysics> droneControllers = new List<SmartDronePhysics>();
    private bool isArming = false;
    private bool isFlying = false;
    private bool isInFormation = false;
    
    private Dictionary<int, Vector3> assignedPositions = new Dictionary<int, Vector3>();
    private HashSet<Vector3> reservedPositions = new HashSet<Vector3>();
    
    // TEKNOFEST PARAMETRELERI
    private float currentFlightAltitude;
    private float currentFormationHoldTime;
    private float currentAgentDistance;
    private bool parametersInitialized = false;
    
    // Components
    private DroneCommHub commHub;
    private FormationGenerator formationGenerator;
    
    void Start()
    {
        commHub = gameObject.AddComponent<DroneCommHub>();
        commHub.Initialize(this);
        
        formationGenerator = gameObject.AddComponent<FormationGenerator>();
        
        InitializeTeknoFestParameters();
        SpawnDronesOnGround();
    }
    
    void Update()
    {
        // TEKNOFEST Parametre Güncelleme
        if (parametreleriGuncelle)
        {
            UpdateTeknoFestParameters();
            parametreleriGuncelle = false;
        }
        
        HandleInputs();
    }
    
    void HandleInputs()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
            StartCoroutine(RestartSystem());
        
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isArming && !isFlying)
            StartCoroutine(ArmAndTakeoff());
        
        if (Keyboard.current.fKey.wasPressedThisFrame && isFlying && !isInFormation && arrowFormationAktif)
            StartCoroutine(FormSmartArrowFormation());
        
        if (Keyboard.current.vKey.wasPressedThisFrame && isFlying && !isInFormation && vFormationAktif)
            StartCoroutine(FormSmartVFormation());
        
        if (Keyboard.current.lKey.wasPressedThisFrame && isFlying && !isInFormation && lineFormationAktif)
            StartCoroutine(FormSmartLineFormation());
        
        if (Keyboard.current.yKey.wasPressedThisFrame && isFlying && !isInFormation && verticalFormationAktif)
            StartCoroutine(FormSmartVerticalLineFormation());
        
        if (Keyboard.current.gKey.wasPressedThisFrame && isFlying)
            StartCoroutine(LandDrones());
    }
    
    void InitializeTeknoFestParameters()
    {
        currentFlightAltitude = ucusIrtifasi;
        currentFormationHoldTime = formasyonKorumaSuresi;
        currentAgentDistance = ajanlarArasiMesafe;
        parametersInitialized = true;
        
        formationHeight = currentFlightAltitude;
        takeoffHeight = currentFlightAltitude - 2f;
        safetyDistance = currentAgentDistance * 0.8f;
        
        Debug.Log($"🏁 TEKNOFEST PARAMETRELERİ: Drone={numberOfDrones} | Z={currentFlightAltitude}m | T={currentFormationHoldTime}s | X={currentAgentDistance}m");
    }
    
    void UpdateTeknoFestParameters()
    {
        Debug.Log("🔄 TEKNOFEST PARAMETRELERİ GÜNCELLENİYOR...");
        
        currentFlightAltitude = ucusIrtifasi;
        currentFormationHoldTime = formasyonKorumaSuresi;
        currentAgentDistance = ajanlarArasiMesafe;
        
        formationHeight = currentFlightAltitude;
        takeoffHeight = currentFlightAltitude - 2f;
        safetyDistance = currentAgentDistance * 0.8f;
        
        Debug.Log($"📏 Yeni Z (İrtifa): {currentFlightAltitude}m");
        Debug.Log($"⏱️ Yeni T (Koruma Süresi): {currentFormationHoldTime}s");  
        Debug.Log($"📐 Yeni X (Ajan Mesafesi): {currentAgentDistance}m");
        Debug.Log($"🚁 Drone Sayısı: {numberOfDrones}");
        Debug.Log("✅ Parametre güncellemesi tamamlandı!");
    }
    
    IEnumerator RestartSystem()
    {
        Debug.Log("=== RESTARTING SYSTEM ===");
        
        foreach (GameObject drone in spawnedDrones)
            if (drone != null) DestroyImmediate(drone);
        
        spawnedDrones.Clear();
        droneControllers.Clear();
        assignedPositions.Clear();
        reservedPositions.Clear();
        isArming = false;
        isFlying = false;
        isInFormation = false;
        
        yield return new WaitForSeconds(0.5f);
        
        InitializeTeknoFestParameters();
        SpawnDronesOnGround();
        Debug.Log($"✅ System restarted with {numberOfDrones} drones!");
    }
    
    void SpawnDronesOnGround()
    {
        if (dronePrefab == null)
        {
            Debug.LogError("Drone Prefab gerekli!");
            return;
        }
        
        spawnedDrones.Clear();
        droneControllers.Clear();
        
        int actualDroneCount = Mathf.Clamp(numberOfDrones, 1, 50);
        int columns = Mathf.CeilToInt(Mathf.Sqrt(actualDroneCount));
        int rows = Mathf.CeilToInt((float)actualDroneCount / columns);
        
        Debug.Log($"🚁 {actualDroneCount} drone spawn ediliyor - Grid: {columns}x{rows}");
        
        int droneIndex = 0;
        for (int row = 0; row < rows && droneIndex < actualDroneCount; row++)
        {
            for (int col = 0; col < columns && droneIndex < actualDroneCount; col++)
            {
                Vector3 position = new Vector3(
                    col * spacingX - (columns * spacingX / 2f),
                    groundHeight,
                    row * spacingZ - (rows * spacingZ / 2f)
                );
                
                int droneNumber = droneIndex + 1;
                GameObject drone = CreateDrone(position, droneNumber);
                spawnedDrones.Add(drone);
                
                SmartDronePhysics physics = drone.AddComponent<SmartDronePhysics>();
                physics.Initialize(thrustForce, moveForce, droneIndex, this, commHub);
                droneControllers.Add(physics);
                
                droneIndex++;
            }
        }
        
        Debug.Log("🧠 INTELLIGENT DRONE SWARM: SPACE=Takeoff | F=Arrow | V=V-Form | L=Line | Y=Vertical | G=Land | R=Restart");
        Debug.Log($"📊 TEKNOFEST: Drone={actualDroneCount} | Z={currentFlightAltitude}m | T={currentFormationHoldTime}s | X={currentAgentDistance}m");
    }
    
    IEnumerator ArmAndTakeoff()
    {
        isArming = true;
        Debug.Log($"🚁 {droneControllers.Count} drone arming...");
        
        for (int i = 0; i < droneControllers.Count; i++)
        {
            droneControllers[i].ArmDrone();
            yield return new WaitForSeconds(0.1f);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("🚀 Takeoff initiated...");
        
        List<int> takeoffOrder = new List<int>();
        for (int i = 0; i < droneControllers.Count; i += 2)
            takeoffOrder.Add(i);
        
        for (int i = 1; i < droneControllers.Count; i += 2)
            takeoffOrder.Add(i);
        
        foreach (int droneIndex in takeoffOrder)
        {
            if (droneIndex < droneControllers.Count)
            {
                droneControllers[droneIndex].SmartTakeOff(currentFlightAltitude - 2f);
                yield return new WaitForSeconds(0.15f);
            }
        }
        
        yield return new WaitForSeconds(3f);
        
        isArming = false;
        isFlying = true;
        Debug.Log($"✈️ {droneControllers.Count} drone swarm airborne!");
    }
    
    // Formation Methods
    IEnumerator FormSmartArrowFormation()
    {
        isInFormation = true;
        Debug.Log($"🏹 ARROW FORMATION - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        Vector3[] arrowPositions = formationGenerator.GenerateDynamicArrowFormation(
            droneControllers.Count, currentFlightAltitude, currentAgentDistance);
        
        yield return StartCoroutine(ExecuteFormation(arrowPositions, "Arrow"));
        
        Debug.Log($"🔒 Arrow formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log("✅ Arrow formation complete!");
    }
    
    IEnumerator FormSmartVFormation()
    {
        isInFormation = true;
        Debug.Log($"📐 V FORMATION - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        Vector3[] vPositions = formationGenerator.GenerateDynamicVFormation(
            droneControllers.Count, currentFlightAltitude, currentAgentDistance);
        
        yield return StartCoroutine(ExecuteFormation(vPositions, "V"));
        
        Debug.Log($"🔒 V formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log("✅ V formation complete!");
    }
    
    IEnumerator FormSmartLineFormation()
    {
        isInFormation = true;
        Debug.Log($"➖ LINE FORMATION - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        Vector3[] linePositions = formationGenerator.GenerateLineFormation(
            droneControllers.Count, currentFlightAltitude, currentAgentDistance);
        
        yield return StartCoroutine(ExecuteFormation(linePositions, "Line"));
        
        Debug.Log($"🔒 Line formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log($"✅ Line formation complete with {droneControllers.Count} drones!");
    }
    
    IEnumerator FormSmartVerticalLineFormation()
    {
        isInFormation = true;
        Debug.Log($"📏 VERTICAL COLUMN - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        Vector3[] columnPositions = formationGenerator.GenerateVerticalFormation(
            droneControllers.Count, currentFlightAltitude, currentAgentDistance);
        
        assignedPositions.Clear();
        for (int i = 0; i < droneControllers.Count; i++)
        {
            assignedPositions[i] = columnPositions[i];
            droneControllers[i].SetAssignedPosition(columnPositions[i]);
        }
        
        Debug.Log($"📏 {droneControllers.Count} drone moving to vertical positions...");
        for (int i = 0; i < droneControllers.Count; i++)
        {
            droneControllers[i].StartSmartFormationMove();
            yield return new WaitForSeconds(0.4f);
        }
        
        yield return new WaitForSeconds(6f);
        
        for (int i = 0; i < droneControllers.Count; i++)
            droneControllers[i].LockFormation();
        
        Debug.Log($"🔒 Vertical formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log($"✅ Vertical formation complete with {droneControllers.Count} drones!");
        
        isInFormation = false;
    }
    
    IEnumerator ExecuteFormation(Vector3[] positions, string formationName)
    {
        int droneCount = droneControllers.Count;
        int positionCount = positions.Length;
        
        Debug.Log($"🎯 {formationName} Formation: {droneCount} drones, {positionCount} positions");
        
        if (positionCount < droneCount)
        {
            Debug.LogError($"❌ {formationName} formasyonu için yeterli pozisyon yok!");
            yield break;
        }
        
        // Staging positions
        Vector3[] stagingPositions = formationGenerator.GenerateCircularStaging(
            positions, droneCount, stagingRadius);
        
        // Phase 1: Staging
        Debug.Log($"🎯 Phase 1: {droneCount} drone staging...");
        for (int i = 0; i < droneCount; i++)
        {
            droneControllers[i].SetStagingPosition(stagingPositions[i]);
            droneControllers[i].MoveToStaging();
            yield return new WaitForSeconds(0.15f);
        }
        
        yield return new WaitForSeconds(3f);
        
        // Phase 2: Formation
        Debug.Log($"🎯 Phase 2: {droneCount} drone formation...");
        assignedPositions.Clear();
        for (int i = 0; i < droneCount; i++)
        {
            assignedPositions[i] = positions[i];
            droneControllers[i].SetAssignedPosition(positions[i]);
        }
        
        int[] smartOrder = GenerateFormationOrder(formationName);
        
        foreach (int droneIndex in smartOrder)
        {
            if (droneIndex < droneCount)
            {
                droneControllers[droneIndex].StartSmartFormationMove();
                yield return new WaitForSeconds(0.25f);
            }
        }
        
        yield return new WaitForSeconds(6f);
        
        for (int i = 0; i < droneCount; i++)
            droneControllers[i].LockFormation();
        
        isInFormation = false;
        Debug.Log($"✅ {formationName} formation complete with {droneCount} drones!");
    }
    
    int[] GenerateFormationOrder(string formationType)
    {
        List<int> order = new List<int>();
        int count = droneControllers.Count;
        
        switch (formationType)
        {
            case "Arrow":
                if (count > 0) order.Add(0);
                if (count > 1) order.Add(count - 1);
                for (int i = 1; i < count - 1; i++)
                    order.Add(i);
                break;
                
            case "Line":
                int center = count / 2;
                order.Add(center);
                for (int i = 1; i <= center; i++)
                {
                    if (center - i >= 0) order.Add(center - i);
                    if (center + i < count) order.Add(center + i);
                }
                break;
                
            default:
                for (int i = 0; i < count; i++)
                    order.Add(i);
                break;
        }
        
        return order.ToArray();
    }
    
    IEnumerator LandDrones()
    {
        Debug.Log("🛬 Landing swarm...");
        isFlying = false;
        isInFormation = false;
        
        foreach (var drone in droneControllers)
        {
            drone.UnlockFormation();
            drone.Land();
            yield return new WaitForSeconds(0.15f);
        }
    }
    
    // API Methods
    public List<SmartDroneData> GetNearbyDroneData(int excludeID, Vector3 position, float range)
    {
        List<SmartDroneData> nearbyDrones = new List<SmartDroneData>();
        
        for (int i = 0; i < droneControllers.Count; i++)
        {
            if (i != excludeID && droneControllers[i] != null)
            {
                float distance = Vector3.Distance(position, droneControllers[i].transform.position);
                if (distance <= range)
                {
                    nearbyDrones.Add(new SmartDroneData
                    {
                        id = i,
                        position = droneControllers[i].transform.position,
                        velocity = droneControllers[i].GetVelocity(),
                        targetPosition = droneControllers[i].GetTargetPosition(),
                        distance = distance
                    });
                }
            }
        }
        
        return nearbyDrones;
    }
    
    public Vector3 GetAssignedPosition(int droneID)
    {
        return assignedPositions.ContainsKey(droneID) ? assignedPositions[droneID] : Vector3.zero;
    }
    
    GameObject CreateDrone(Vector3 position, int droneNumber)
    {
        GameObject newDrone = Instantiate(dronePrefab, position, Quaternion.identity);
        newDrone.name = "SmartDrone_" + droneNumber;
        
        Rigidbody rb = newDrone.GetComponent<Rigidbody>();
        if (rb == null) rb = newDrone.AddComponent<Rigidbody>();
        
        rb.mass = 1f;
        rb.linearDamping = 4f;
        rb.angularDamping = 10f;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        return newDrone;
    }
    
    // Getters
    public float GetCurrentFlightAltitude() => currentFlightAltitude;
    public float GetCurrentAgentDistance() => currentAgentDistance;
    public float GetCurrentFormationHoldTime() => currentFormationHoldTime;
}