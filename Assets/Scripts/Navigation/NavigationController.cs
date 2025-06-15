using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

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
    
    [Header("5.2 SÜRÜ HALİNDE NAVİGASYON PARAMETRELERİ")]
    [Tooltip("T1 - Ara Nokta Ulaşma Süresi (saniye)")]
    public float araNokta_UlasmaSuresi_T1 = 10f;
    
    [Tooltip("T2 - Ara Noktada Bekleme Süresi (saniye)")]
    public float araNokta_BeklemeSuresi_T2 = 15f;
    
    [Tooltip("Navigasyon Waypoint'leri (Transform'ları sahne içine yerleştirin)")]
    public Transform[] navigationWaypoints = new Transform[0];
    
    [Tooltip("Hedef Landing Noktası")]
    public Transform landingTarget;
    
    [Tooltip("Haberleşme Kesintisi Simülasyonu (saniye cinsinden)")]
    public float haberlesmeKesintisiSuresi = 0f;
    
    [Space(5)]
    [Header("YARIŞMA SENARYOLARI")]
    public bool arrowFormationAktif = true;
    public bool vFormationAktif = true;
    public bool lineFormationAktif = true;
    public bool verticalFormationAktif = true;
    public bool navigationMissionAktif = true;
    
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
    
    private List<GameObject> spawnedDrones = new List<GameObject>();
    private List<SmartDronePhysics> droneControllers = new List<SmartDronePhysics>();
    private bool isArming = false;
    private bool isFlying = false;
    private bool isInFormation = false;
    private bool isNavigating = false;
    private bool communicationLost = false;
    
    private Dictionary<int, Vector3> assignedPositions = new Dictionary<int, Vector3>();
    private HashSet<Vector3> reservedPositions = new HashSet<Vector3>();
    
    // TEKNOFEST PARAMETRELERI
    private float currentFlightAltitude;
    private float currentFormationHoldTime;
    private float currentAgentDistance;
    private float currentT1_ReachTime;
    private float currentT2_WaitTime;
    private bool parametersInitialized = false;
    
    // NAVİGASYON SİSTEMİ
    private Vector3[] currentFormationOffsets;
    private string currentFormationType = "";
    private int currentWaypointIndex = 0;
    private Vector3 formationCenter = Vector3.zero;
    private bool waypointReached = false;
    private float waypointTimer = 0f;
    private float navigationStartTime = 0f;
    
    // HABERLEŞME KESİNTİSİ
    private bool communicationCutSimulated = false;
    private float communicationCutTimer = 0f;
    
    private DroneCommHub commHub;
    
    void Start()
    {
        commHub = gameObject.AddComponent<DroneCommHub>();
        commHub.Initialize(this);
        InitializeTeknoFestParameters();
        SpawnDronesOnGround();
        CreateDefaultWaypoints();
    }
    
    void Update()
    {
        // TEKNOFEST Parametre Güncelleme - Her frame kontrol et
        if (parametreleriGuncelle)
        {
            UpdateTeknoFestParameters();
            parametreleriGuncelle = false;
        }
        
        // HABERLEŞİME KESİNTİSİ SİMÜLASYONU
        if (haberlesmeKesintisiSuresi > 0 && !communicationCutSimulated && isNavigating)
        {
            communicationCutTimer += Time.deltaTime;
            if (communicationCutTimer >= haberlesmeKesintisiSuresi)
            {
                StartCommunicationCut();
                communicationCutSimulated = true;
            }
        }
        
        // NAVİGASYON SİSTEMİ UPDATE
        if (isNavigating)
        {
            UpdateNavigationSystem();
        }
        
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
        
        // 🆕 NAVİGASYON GÖREVİ BAŞLATMA - DEBUG ile
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            Debug.Log($"🔍 N tuşu basıldı - Durum kontrol: isFlying={isFlying}, isInFormation={isInFormation}, isNavigating={isNavigating}, navigationMissionAktif={navigationMissionAktif}");
            
            if (!isFlying)
                Debug.LogWarning("❌ Önce drone'ları kaldırın! (SPACE tuşu)");
            else if (isNavigating)
                Debug.LogWarning("❌ Navigasyon zaten devam ediyor!");
            else if (!navigationMissionAktif)
                Debug.LogWarning("❌ Navigation Mission aktif değil!");
            else if (!isInFormation)
                Debug.LogWarning("⚠️ Formasyon modu aktif değil, yine de navigasyon başlatılıyor...");
            
            // Şartları gevşetiyoruz - sadece uçuyor olmaları yeterli
            if (isFlying && !isNavigating && navigationMissionAktif)
                StartCoroutine(StartNavigationMission());
        }
        
        // 🆕 HABERLEŞİME KESİNTİSİ MANUEL TEST - DEBUG ile
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log($"🔍 C tuşu basıldı - Durum: isNavigating={isNavigating}, communicationLost={communicationLost}");
            
            if (!isNavigating)
                Debug.LogWarning("❌ Önce navigasyon görevini başlatın! (N tuşu)");
            else if (communicationLost)
                Debug.LogWarning("❌ Haberleşme zaten kesilmiş!");
            else
                StartCommunicationCut();
        }
        
        if (Keyboard.current.gKey.wasPressedThisFrame && isFlying)
            StartCoroutine(LandDrones());
    }
    
    void CreateDefaultWaypoints()
    {
        if (navigationWaypoints.Length == 0)
        {
            // Eğer waypoint tanımlanmamışsa default waypoint'ler oluştur
            GameObject waypointsParent = new GameObject("NavigationWaypoints");
            
            List<Transform> waypoints = new List<Transform>();
            
            // Waypoint 1 - Sağ ön
            GameObject wp1 = new GameObject("Waypoint_1");
            wp1.transform.parent = waypointsParent.transform;
            wp1.transform.position = new Vector3(20, currentFlightAltitude, 20);
            waypoints.Add(wp1.transform);
            
            // Waypoint 2 - Sol ön
            GameObject wp2 = new GameObject("Waypoint_2");
            wp2.transform.parent = waypointsParent.transform;
            wp2.transform.position = new Vector3(-20, currentFlightAltitude, 40);
            waypoints.Add(wp2.transform);
            
            // Landing Target
            if (landingTarget == null)
            {
                GameObject landingGO = new GameObject("LandingTarget");
                landingGO.transform.parent = waypointsParent.transform;
                landingGO.transform.position = new Vector3(0, 1f, 60);
                landingTarget = landingGO.transform;
            }
            
            navigationWaypoints = waypoints.ToArray();
            
            Debug.Log($"🎯 {waypoints.Count} default waypoint oluşturuldu!");
        }
    }
    
    void InitializeTeknoFestParameters()
    {
        currentFlightAltitude = ucusIrtifasi;
        currentFormationHoldTime = formasyonKorumaSuresi;
        currentAgentDistance = ajanlarArasiMesafe;
        currentT1_ReachTime = araNokta_UlasmaSuresi_T1;
        currentT2_WaitTime = araNokta_BeklemeSuresi_T2;
        parametersInitialized = true;
        
        formationHeight = currentFlightAltitude;
        takeoffHeight = currentFlightAltitude - 2f;
        safetyDistance = currentAgentDistance * 0.8f;
        
        Debug.Log($"🏁 TEKNOFEST PARAMETRELERİ: Drone={numberOfDrones} | Z={currentFlightAltitude}m | T={currentFormationHoldTime}s | X={currentAgentDistance}m");
        Debug.Log($"🚁 NAVİGASYON: T1={currentT1_ReachTime}s | T2={currentT2_WaitTime}s | Waypoints={navigationWaypoints.Length}");
    }
    
    void UpdateTeknoFestParameters()
    {
        Debug.Log("🔄 TEKNOFEST PARAMETRELERİ GÜNCELLENİYOR...");
        
        currentFlightAltitude = ucusIrtifasi;
        currentFormationHoldTime = formasyonKorumaSuresi;
        currentAgentDistance = ajanlarArasiMesafe;
        currentT1_ReachTime = araNokta_UlasmaSuresi_T1;
        currentT2_WaitTime = araNokta_BeklemeSuresi_T2;
        
        formationHeight = currentFlightAltitude;
        takeoffHeight = currentFlightAltitude - 2f;
        safetyDistance = currentAgentDistance * 0.8f;
        
        Debug.Log($"📏 Yeni Z (İrtifa): {currentFlightAltitude}m");
        Debug.Log($"⏱️ Yeni T (Koruma Süresi): {currentFormationHoldTime}s");  
        Debug.Log($"📐 Yeni X (Ajan Mesafesi): {currentAgentDistance}m");
        Debug.Log($"🎯 Yeni T1 (Ulaşma): {currentT1_ReachTime}s | T2 (Bekleme): {currentT2_WaitTime}s");
        Debug.Log($"🚁 Drone Sayısı: {numberOfDrones}");
        Debug.Log("✅ Parametre güncellemesi tamamlandı!");
    }
    
    // 🆕 NAVİGASYON SİSTEMİ
    IEnumerator StartNavigationMission()
    {
        Debug.Log("🎯 NAVİGASYON GÖREVİ BAŞLATMA İSTEĞİ ALINDI!");
        
        if (navigationWaypoints == null || navigationWaypoints.Length == 0)
        {
            Debug.LogError("❌ Waypoint'ler tanımlanmamış! Default waypoint'ler oluşturuluyor...");
            CreateDefaultWaypoints();
            
            if (navigationWaypoints == null || navigationWaypoints.Length == 0)
            {
                Debug.LogError("❌ Default waypoint oluşturulamadı! Navigasyon iptal ediliyor.");
                yield break;
            }
        }
        
        // Waypoint'lerin geçerli olduğunu kontrol et
        for (int i = 0; i < navigationWaypoints.Length; i++)
        {
            if (navigationWaypoints[i] == null)
            {
                Debug.LogError($"❌ Waypoint {i} null! Navigasyon iptal ediliyor.");
                yield break;
            }
        }
        
        isNavigating = true;
        currentWaypointIndex = 0;
        communicationCutTimer = 0f;
        communicationCutSimulated = false;
        communicationLost = false;
        navigationStartTime = Time.time;
        
        // Mevcut formasyonu kaydet
        SaveCurrentFormation();
        
        Debug.Log($"🎯 NAVİGASYON GÖREVİ BAŞLATILIYOR!");
        Debug.Log($"📊 Parametreler: T1={currentT1_ReachTime}s | T2={currentT2_WaitTime}s");
        Debug.Log($"🗺️ Rota: {navigationWaypoints.Length} waypoint + Landing");
        Debug.Log($"📡 Haberleşme kesintisi: {haberlesmeKesintisiSuresi}s sonra");
        
        // İlk waypoint'e hareket başlat
        StartMoveToWaypoint(0);
    }
    
    void SaveCurrentFormation()
    {
        if (droneControllers == null || droneControllers.Count == 0)
        {
            Debug.LogError("❌ Drone controller'lar bulunamadı!");
            return;
        }
        
        currentFormationOffsets = new Vector3[droneControllers.Count];
        formationCenter = CalculateFormationCenter();
        
        for (int i = 0; i < droneControllers.Count; i++)
        {
            if (droneControllers[i] != null)
            {
                currentFormationOffsets[i] = droneControllers[i].transform.position - formationCenter;
            }
            else
            {
                Debug.LogWarning($"⚠️ Drone {i} controller null!");
                currentFormationOffsets[i] = Vector3.zero;
            }
        }
        
        Debug.Log($"💾 Formasyon kaydedildi: Merkez={formationCenter}, Drone sayısı={droneControllers.Count}");
        
        // Formasyon offset'lerini logla
        for (int i = 0; i < currentFormationOffsets.Length; i++)
        {
            Debug.Log($"   Drone {i}: Offset={currentFormationOffsets[i]}");
        }
    }
    
    Vector3 CalculateFormationCenter()
    {
        if (droneControllers.Count == 0) return Vector3.zero;
        
        Vector3 center = Vector3.zero;
        foreach (var drone in droneControllers)
        {
            center += drone.transform.position;
        }
        return center / droneControllers.Count;
    }
    
    void StartMoveToWaypoint(int waypointIndex)
    {
        if (waypointIndex >= navigationWaypoints.Length) return;
        
        waypointReached = false;
        waypointTimer = 0f;
        
        Vector3 targetWaypoint = navigationWaypoints[waypointIndex].position;
        
        Debug.Log($"🎯 Waypoint {waypointIndex + 1}/{navigationWaypoints.Length} hedefleniyor: {targetWaypoint}");
        Debug.Log($"⏱️ Maksimum ulaşma süresi: {currentT1_ReachTime} saniye");
        
        // Formasyon merkezini waypoint'e taşı
        formationCenter = targetWaypoint;
        
        // Her drone'a yeni pozisyonunu ata
        for (int i = 0; i < droneControllers.Count; i++)
        {
            Vector3 newTargetPosition = formationCenter + currentFormationOffsets[i];
            assignedPositions[i] = newTargetPosition;
            droneControllers[i].SetAssignedPosition(newTargetPosition);
            droneControllers[i].StartNavigationMove(currentT1_ReachTime);
        }
    }
    
    void UpdateNavigationSystem()
    {
        waypointTimer += Time.deltaTime;
        
        // Tüm drone'lar waypoint'e ulaştı mı kontrol et
        bool allDronesReached = true;
        foreach (var drone in droneControllers)
        {
            if (!drone.IsAtAssignedPosition(2f)) // 2 metre tolerans
            {
                allDronesReached = false;
                break;
            }
        }
        
        // Waypoint'e ulaşıldı
        if (allDronesReached && !waypointReached)
        {
            waypointReached = true;
            waypointTimer = 0f;
            
            Debug.Log($"✅ Waypoint {currentWaypointIndex + 1} ulaşıldı! Bekleme süresi: {currentT2_WaitTime}s");
            
            // Formasyonu kilitle
            foreach (var drone in droneControllers)
            {
                drone.LockFormation();
            }
        }
        
        // Bekleme süresi doldu, bir sonraki waypoint'e geç
        if (waypointReached && waypointTimer >= currentT2_WaitTime)
        {
            currentWaypointIndex++;
            
            if (currentWaypointIndex >= navigationWaypoints.Length)
            {
                // Tüm waypoint'ler tamamlandı, landing'e geç
                StartCoroutine(NavigateToLanding());
            }
            else
            {
                // Bir sonraki waypoint'e hareket et
                StartMoveToWaypoint(currentWaypointIndex);
            }
        }
        
        // T1 süresi aştı ama waypoint'e ulaşamadı (hata durumu)
        if (!waypointReached && waypointTimer > currentT1_ReachTime)
        {
            Debug.LogWarning($"⚠️ T1 süresi aşıldı! Waypoint {currentWaypointIndex + 1} ulaşılamadı.");
            // Yine de devam et
            waypointReached = true;
            waypointTimer = 0f;
        }
    }
    
    IEnumerator NavigateToLanding()
    {
        Debug.Log("🛬 LANDING PHASE - Hedef noktaya iniş başlatılıyor...");
        
        if (landingTarget != null)
        {
            // Landing pozisyonuna formasyonu taşı
            formationCenter = new Vector3(landingTarget.position.x, currentFlightAltitude, landingTarget.position.z);
            
            for (int i = 0; i < droneControllers.Count; i++)
            {
                Vector3 landingPosition = formationCenter + currentFormationOffsets[i];
                landingPosition.y = landingTarget.position.y; // Landing yüksekliği
                
                assignedPositions[i] = landingPosition;
                droneControllers[i].SetAssignedPosition(landingPosition);
                droneControllers[i].StartNavigationMove(currentT1_ReachTime);
            }
            
            yield return new WaitForSeconds(2f);
            
            // Formasyon halinde iniş
            foreach (var drone in droneControllers)
            {
                drone.NavigationLand();
                yield return new WaitForSeconds(0.2f);
            }
            
            yield return new WaitForSeconds(5f);
        }
        
        float totalTime = Time.time - navigationStartTime;
        Debug.Log($"🏁 NAVİGASYON GÖREVİ TAMAMLANDI!");
        Debug.Log($"📊 Toplam süre: {totalTime:F1}s | Waypoints: {navigationWaypoints.Length} | Haberleşme kesintisi: {(communicationLost ? "EVET" : "HAYIR")}");
        
        isNavigating = false;
        isInFormation = false;
        isFlying = false;
    }
    
    void StartCommunicationCut()
    {
        communicationLost = true;
        Debug.Log("📡❌ HABERLEŞİME KESİNTİSİ! Sürü otonom moda geçiyor...");
        
        // Tüm drone'lara otonom mod komutunu gönder
        foreach (var drone in droneControllers)
        {
            drone.SetAutonomousMode(true);
        }
        
        // Communication hub'ı devre dışı bırak
        if (commHub != null)
        {
            commHub.SetCommunicationStatus(false);
        }
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
        isNavigating = false;
        communicationLost = false;
        communicationCutSimulated = false;
        currentWaypointIndex = 0;
        
        yield return new WaitForSeconds(0.5f);
        
        InitializeTeknoFestParameters(); // Parametreleri yeniden yükle
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
        
        // DİNAMİK DRONE SAYISI - numberOfDrones parametresini kullan
        int actualDroneCount = Mathf.Clamp(numberOfDrones, 1, 50); // Maksimum 50 drone
        
        // Drone'ları grid formatında yerleştir
        int columns = Mathf.CeilToInt(Mathf.Sqrt(actualDroneCount)); // Kare şeklinde grid
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
        
        Debug.Log("🧠 INTELLIGENT DRONE SWARM CONTROLS:");
        Debug.Log("   SPACE=Takeoff | F=Arrow | V=V-Form | L=Line | Y=Vertical");
        Debug.Log("   N=Navigation Mission | C=Cut Communication | G=Land | R=Restart");
        Debug.Log($"📊 TEKNOFEST: Drone={actualDroneCount} | Z={currentFlightAltitude}m | T={currentFormationHoldTime}s | X={currentAgentDistance}m");
        Debug.Log($"🎯 NAVİGASYON: T1={currentT1_ReachTime}s | T2={currentT2_WaitTime}s | Waypoints={navigationWaypoints.Length}");
        Debug.Log($"🔧 DURUM: Flying={isFlying} | Formation={isInFormation} | Navigating={isNavigating}");
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
        
        // DİNAMİK TAKEOFF ORDER - Drone sayısına göre ayarla
        List<int> takeoffOrder = new List<int>();
        
        // Dış kenardan başla, merkeze doğru git
        for (int i = 0; i < droneControllers.Count; i += 2)
            takeoffOrder.Add(i); // Çift indexler önce
        
        for (int i = 1; i < droneControllers.Count; i += 2)
            takeoffOrder.Add(i); // Tek indexler sonra
        
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
    
    IEnumerator FormSmartArrowFormation()
    {
        isInFormation = true;
        currentFormationType = "Arrow";
        Debug.Log($"🏹 ARROW FORMATION - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        // DİNAMİK ARROW FORMASYONU - Drone sayısına göre hesapla
        Vector3[] arrowPositions = GenerateDynamicArrowFormation();
        
        yield return StartCoroutine(ExecuteFormation(arrowPositions, "Arrow"));
        
        Debug.Log($"🔒 Arrow formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log("✅ Arrow formation complete!");
    }
    
    Vector3[] GenerateDynamicArrowFormation()
    {
        int droneCount = droneControllers.Count;
        Vector3[] positions = new Vector3[droneCount];
        
        // Ok başı (tip)
        positions[0] = new Vector3(0, currentFlightAltitude + 6, 0);
        
        if (droneCount == 1) return positions;
        
        // Ok kuyruğu (en arkada)
        if (droneCount > 1)
            positions[droneCount - 1] = new Vector3(0, currentFlightAltitude - 4, -(droneCount * 2));
        
        // Yan kanatlar - dengeli dağıtım
        int sidesCount = droneCount - 2; // Tip ve kuyruk hariç
        int leftWing = sidesCount / 2;
        int rightWing = sidesCount - leftWing;
        
        // Sol kanat
        for (int i = 0; i < leftWing; i++)
        {
            float wingStep = (float)(i + 1) / (leftWing + 1);
            positions[i + 1] = new Vector3(
                -currentAgentDistance * (i + 1),
                currentFlightAltitude + 4 - (wingStep * 8),
                -(wingStep * droneCount * 1.5f)
            );
        }
        
        // Sağ kanat
        for (int i = 0; i < rightWing; i++)
        {
            float wingStep = (float)(i + 1) / (rightWing + 1);
            positions[leftWing + i + 1] = new Vector3(
                currentAgentDistance * (i + 1),
                currentFlightAltitude + 4 - (wingStep * 8),
                -(wingStep * droneCount * 1.5f)
            );
        }
        
        return positions;
    }
    
    IEnumerator FormSmartVFormation()
    {
        isInFormation = true;
        currentFormationType = "V";
        Debug.Log($"📐 V FORMATION - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        // DİNAMİK V FORMASYONU - Drone sayısına göre hesapla
        Vector3[] vPositions = GenerateDynamicVFormation();
        
        yield return StartCoroutine(ExecuteFormation(vPositions, "V"));
        
        Debug.Log($"🔒 V formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log("✅ V formation complete!");
    }
    
    Vector3[] GenerateDynamicVFormation()
    {
        int droneCount = droneControllers.Count;
        Vector3[] positions = new Vector3[droneCount];
        
        // V'nin alt merkez noktası
        positions[0] = new Vector3(0, currentFlightAltitude, 0);
        
        if (droneCount == 1) return positions;
        
        // Kalan drone'ları sol ve sağ kanatlara dağıt
        int sidesCount = droneCount - 1;
        int leftWing = sidesCount / 2;
        int rightWing = sidesCount - leftWing;
        
        // Sol kanat (yükseklik artarak)
        for (int i = 0; i < leftWing; i++)
        {
            positions[i + 1] = new Vector3(
                -currentAgentDistance * (i + 1) * 0.8f,
                currentFlightAltitude + ((i + 1) * 2.5f),
                0
            );
        }
        
        // Sağ kanat (yükseklik artarak)
        for (int i = 0; i < rightWing; i++)
        {
            positions[leftWing + i + 1] = new Vector3(
                currentAgentDistance * (i + 1) * 0.8f,
                currentFlightAltitude + ((i + 1) * 2.5f),
                0
            );
        }
        
        return positions;
    }
    
    IEnumerator FormSmartLineFormation()
    {
        isInFormation = true;
        currentFormationType = "Line";
        Debug.Log($"➖ LINE FORMATION - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        // DİNAMİK LINE FORMASYONU - Tüm drone'lar için
        Vector3[] linePositions = new Vector3[droneControllers.Count];
        float totalWidth = (droneControllers.Count - 1) * currentAgentDistance;
        float startX = -totalWidth / 2f;
        
        for (int i = 0; i < droneControllers.Count; i++)
        {
            linePositions[i] = new Vector3(
                startX + (i * currentAgentDistance),
                currentFlightAltitude,
                0
            );
        }
        
        yield return StartCoroutine(ExecuteFormation(linePositions, "Line"));
        
        Debug.Log($"🔒 Line formation koruma: {currentFormationHoldTime}s");
        yield return new WaitForSeconds(currentFormationHoldTime);
        Debug.Log($"✅ Line formation complete with {droneControllers.Count} drones!");
    }
    
    IEnumerator FormSmartVerticalLineFormation()
    {
        isInFormation = true;
        currentFormationType = "Vertical";
        Debug.Log($"📏 VERTICAL COLUMN - {droneControllers.Count} drones, Z:{currentFlightAltitude}m, X:{currentAgentDistance}m");
        
        // DİNAMİK VERTICAL COLUMN - Tüm drone'lar için
        Vector3[] columnPositions = new Vector3[droneControllers.Count];
        
        for (int i = 0; i < droneControllers.Count; i++)
        {
            columnPositions[i] = new Vector3(
                0f,
                currentFlightAltitude + (i * currentAgentDistance * 0.6f),
                0f
            );
        }
        
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
        
        // Eğer pozisyon sayısı drone sayısından azsa, hata ver
        if (positionCount < droneCount)
        {
            Debug.LogError($"❌ {formationName} formasyonu için yeterli pozisyon yok! Drone:{droneCount}, Position:{positionCount}");
            yield break;
        }
        
        // Staging positions - drone sayısına göre dinamik
        Vector3[] stagingPositions = new Vector3[droneCount];
        Vector3 center = Vector3.zero;
        for (int i = 0; i < droneCount && i < positionCount; i++) 
            center += positions[i];
        center /= Mathf.Min(droneCount, positionCount);
        
        float angleStep = 360f / (float)droneCount;
        for (int i = 0; i < droneCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            stagingPositions[i] = new Vector3(
                center.x + stagingRadius * Mathf.Cos(angle),
                center.y,
                center.z + stagingRadius * Mathf.Sin(angle)
            );
        }
        
        // Phase 1: Staging - TÜM DRONE'LAR
        Debug.Log($"🎯 Phase 1: {droneCount} drone staging...");
        for (int i = 0; i < droneCount; i++)
        {
            droneControllers[i].SetStagingPosition(stagingPositions[i]);
            droneControllers[i].MoveToStaging();
            yield return new WaitForSeconds(0.15f);
        }
        
        yield return new WaitForSeconds(3f);
        
        // Phase 2: Formation - TÜM DRONE'LAR
        Debug.Log($"🎯 Phase 2: {droneCount} drone formation...");
        assignedPositions.Clear();
        for (int i = 0; i < droneCount; i++)
        {
            assignedPositions[i] = positions[i];
            droneControllers[i].SetAssignedPosition(positions[i]);
        }
        
        int[] smartOrder = formationName == "Arrow" ? GenerateArrowOrder() :
                          formationName == "Line" ? GenerateCenterOutOrder() :
                          GenerateSequentialOrder();
        
        foreach (int droneIndex in smartOrder)
        {
            if (droneIndex < droneCount)
            {
                droneControllers[droneIndex].StartSmartFormationMove();
                yield return new WaitForSeconds(0.25f);
            }
        }
        
        yield return new WaitForSeconds(6f);
        
        // TÜM DRONE'LARI kilitle
        for (int i = 0; i < droneCount; i++)
            droneControllers[i].LockFormation();
        
        isInFormation = false;
        Debug.Log($"✅ {formationName} formation complete with {droneCount} drones!");
    }
    
    int[] GenerateArrowOrder()
    {
        List<int> order = new List<int>();
        int count = droneControllers.Count;
        
        // Tip önce (ilk drone)
        if (count > 0) order.Add(0);
        
        // Kuyruk sonra (son drone)  
        if (count > 1) order.Add(count - 1);
        
        // Diğerleri sırayla - önce sol kanat, sonra sağ kanat
        for (int i = 1; i < count - 1; i++)
            order.Add(i);
        
        return order.ToArray();
    }
    
    int[] GenerateSequentialOrder()
    {
        int[] order = new int[droneControllers.Count];
        for (int i = 0; i < droneControllers.Count; i++)
            order[i] = i;
        return order;
    }
    
    int[] GenerateCenterOutOrder()
    {
        List<int> order = new List<int>();
        int count = droneControllers.Count;
        int center = count / 2;
        
        order.Add(center);
        for (int i = 1; i <= center; i++)
        {
            if (center - i >= 0) order.Add(center - i);
            if (center + i < count) order.Add(center + i);
        }
        
        return order.ToArray();
    }
    
    IEnumerator LandDrones()
    {
        Debug.Log("🛬 Landing swarm...");
        isFlying = false;
        isInFormation = false;
        isNavigating = false;
        
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
    
    public bool IsCommunicationLost() { return communicationLost; }
    
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
}

[System.Serializable]
public class SmartDroneData
{
    public int id;
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 targetPosition;
    public float distance;
}

public class DroneCommHub : MonoBehaviour
{
    private DroneSpawner spawner;
    private bool communicationActive = true;
    
    public void Initialize(DroneSpawner droneSpawner)
    {
        spawner = droneSpawner;
    }
    
    public void SetCommunicationStatus(bool status)
    {
        communicationActive = status;
        Debug.Log($"📡 Communication Hub: {(status ? "ACTIVE" : "OFFLINE")}");
    }
    
    public Vector3 RequestSafePathVector(int droneID, Vector3 currentPos, Vector3 targetPos)
    {
        // Haberleşme kesintisi durumunda basit collision avoidance
        if (!communicationActive)
        {
            return GetBasicAvoidanceVector(droneID, currentPos, targetPos);
        }
        
        List<SmartDroneData> nearbyDrones = spawner.GetNearbyDroneData(droneID, currentPos, 10f);
        
        Vector3 safePath = targetPos - currentPos;
        Vector3 avoidanceVector = Vector3.zero;
        
        foreach (SmartDroneData drone in nearbyDrones)
        {
            if (drone.distance < 5f)
            {
                Vector3 futurePos = drone.position + drone.velocity * 2f;
                Vector3 avoidDirection = (currentPos - futurePos).normalized;
                
                float avoidanceStrength = (5f - drone.distance) / 5f;
                avoidanceVector += avoidDirection * avoidanceStrength * 3f;
            }
        }
        
        return (safePath.normalized + avoidanceVector.normalized).normalized;
    }
    
    Vector3 GetBasicAvoidanceVector(int droneID, Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 direction = (targetPos - currentPos).normalized;
        Vector3 avoidance = Vector3.zero;
        
        // Sadece yakın drone'lardan kaçın
        List<SmartDroneData> nearbyDrones = spawner.GetNearbyDroneData(droneID, currentPos, 6f);
        
        foreach (SmartDroneData drone in nearbyDrones)
        {
            if (drone.distance < 4f)
            {
                Vector3 avoidDir = (currentPos - drone.position).normalized;
                avoidance += avoidDir * (4f - drone.distance);
            }
        }
        
        return (direction + avoidance * 0.3f).normalized;
    }
}

public class SmartDronePhysics : MonoBehaviour
{
    public float thrustForce = 12f;
    public float moveForce = 10f;
    public float hoverThrust = 10f;
    public float maxSpeed = 14f;
    public float fastModeMultiplier = 2f;
    
    [System.NonSerialized] public int droneID;
    
    private DroneSpawner swarmController;
    private DroneCommHub commHub;
    private Rigidbody rb;
    
    private enum DroneState { 
        Grounded, Armed, TakingOff, Hovering, Staging, FormationMove, 
        FastFormationMove, FormationHold, NavigationMove, NavigationLanding, Landing 
    }
    
    private DroneState currentState = DroneState.Grounded;
    private Vector3 assignedPosition;
    private Vector3 stagingPosition;
    private float targetHeight;
    private bool autonomousMode = false;
    private float navigationMoveTimer = 0f;
    private float maxNavigationTime = 10f;
    
    public void Initialize(float thrust, float move, int id, DroneSpawner controller, DroneCommHub hub)
    {
        thrustForce = thrust;
        moveForce = move;
        droneID = id;
        swarmController = controller;
        commHub = hub;
        
        rb = GetComponent<Rigidbody>();
        targetHeight = transform.position.y;
    }
    
    void FixedUpdate()
    {
        if (currentState == DroneState.Grounded) return;
        
        float upwardThrust = CalculateIntelligentThrust();
        rb.AddForce(Vector3.up * upwardThrust);
        
        ProcessIntelligentMovement();
        
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }
    
    void ProcessIntelligentMovement()
    {
        Vector3 currentPos = transform.position;
        Vector3 targetForce = Vector3.zero;
        
        switch (currentState)
        {
            case DroneState.Staging:
                targetForce = CalculateSmartMovement(currentPos, stagingPosition);
                CheckWaypointReached(stagingPosition, 2f);
                break;
                
            case DroneState.FormationMove:
                targetForce = CalculateSmartMovement(currentPos, assignedPosition);
                CheckWaypointReached(assignedPosition, 1.5f);
                break;
                
            case DroneState.FastFormationMove:
                targetForce = CalculateFastMovement(currentPos, assignedPosition);
                CheckWaypointReached(assignedPosition, 1.8f);
                break;
                
            case DroneState.NavigationMove:
                navigationMoveTimer += Time.fixedDeltaTime;
                targetForce = autonomousMode ? 
                    CalculateAutonomousMovement(currentPos, assignedPosition) :
                    CalculateSmartMovement(currentPos, assignedPosition);
                CheckWaypointReached(assignedPosition, 2f);
                break;
                
            case DroneState.FormationHold:
                targetForce = CalculatePrecisionHold(currentPos, assignedPosition);
                break;
        }
        
        rb.AddForce(targetForce);
    }
    
    Vector3 CalculateSmartMovement(Vector3 from, Vector3 to)
    {
        if (commHub == null) return (to - from).normalized * moveForce;
        
        Vector3 safeDirection = commHub.RequestSafePathVector(droneID, from, to);
        float distance = Vector3.Distance(from, to);
        float forceMultiplier = Mathf.Clamp(distance / 2f, 0.8f, 2f);
        
        return safeDirection * moveForce * forceMultiplier;
    }
    
    Vector3 CalculateAutonomousMovement(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);
        
        // Otonom modda basit collision avoidance
        Vector3 avoidance = CalculateBasicAvoidance(from);
        
        float forceMultiplier = Mathf.Clamp(distance / 2f, 0.8f, 2f);
        return (direction + avoidance * 0.4f).normalized * moveForce * forceMultiplier;
    }
    
    Vector3 CalculateFastMovement(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);
        Vector3 avoidance = CalculateBasicAvoidance(from);
        
        float forceMultiplier = Mathf.Clamp(distance / 1.5f, 1f, 2.5f);
        return (direction + avoidance * 0.2f).normalized * moveForce * fastModeMultiplier * forceMultiplier;
    }
    
    Vector3 CalculateBasicAvoidance(Vector3 myPos)
    {
        if (swarmController == null) return Vector3.zero;
        
        Vector3 avoidance = Vector3.zero;
        List<SmartDroneData> nearby = swarmController.GetNearbyDroneData(droneID, myPos, 4f);
        
        foreach (SmartDroneData drone in nearby)
        {
            if (drone.distance < 3f)
            {
                Vector3 away = (myPos - drone.position).normalized;
                avoidance += away * (3f - drone.distance);
            }
        }
        
        return avoidance;
    }
    
    Vector3 CalculatePrecisionHold(Vector3 from, Vector3 to)
    {
        Vector3 error = to - from;
        float errorMagnitude = error.magnitude;
        
        if (errorMagnitude < 0.1f) return Vector3.zero;
        
        float force = Mathf.Clamp(errorMagnitude * 2f, 0.1f, moveForce * 0.5f);
        return error.normalized * force;
    }
    
    void CheckWaypointReached(Vector3 target, float threshold)
    {
        if (Vector3.Distance(transform.position, target) < threshold)
        {
            switch (currentState)
            {
                case DroneState.Staging:
                    currentState = DroneState.Hovering;
                    break;
                    
                case DroneState.FormationMove:
                case DroneState.FastFormationMove:
                case DroneState.NavigationMove:
                    currentState = DroneState.Hovering;
                    targetHeight = assignedPosition.y;
                    navigationMoveTimer = 0f;
                    break;
            }
        }
    }
    
    float CalculateIntelligentThrust()
    {
        float currentHeight = transform.position.y;
        float thrust = hoverThrust;
        
        switch (currentState)
        {
            case DroneState.TakingOff:
                if (currentHeight < targetHeight - 0.5f)
                    thrust = thrustForce * 1.2f;
                else
                    currentState = DroneState.Hovering;
                break;
                
            case DroneState.Landing:
            case DroneState.NavigationLanding:
                thrust = hoverThrust * 0.3f;
                if (currentHeight <= 1.5f)
                {
                    currentState = DroneState.Grounded;
                    rb.linearVelocity = Vector3.zero;
                }
                break;
                
            case DroneState.FormationHold:
                float heightError = assignedPosition.y - currentHeight;
                thrust = hoverThrust + (heightError * 5f);
                thrust = Mathf.Clamp(thrust, hoverThrust * 0.6f, hoverThrust * 1.8f);
                break;
                
            default:
                float heightError2 = targetHeight - currentHeight;
                thrust = hoverThrust + (heightError2 * 3f);
                thrust = Mathf.Clamp(thrust, hoverThrust * 0.7f, hoverThrust * 1.4f);
                break;
        }
        
        return thrust;
    }
    
    // Public Methods
    public void ArmDrone() { currentState = DroneState.Armed; }
    
    public void SmartTakeOff(float height)
    {
        if (currentState != DroneState.Armed) return;
        targetHeight = height;
        currentState = DroneState.TakingOff;
    }
    
    public void SetStagingPosition(Vector3 position) { stagingPosition = position; }
    
    public void MoveToStaging()
    {
        if (currentState == DroneState.Grounded) return;
        currentState = DroneState.Staging;
        targetHeight = stagingPosition.y;
    }
    
    public void SetAssignedPosition(Vector3 position) { assignedPosition = position; }
    
    public void StartSmartFormationMove()
    {
        if (currentState == DroneState.Grounded) return;
        currentState = DroneState.FormationMove;
    }
    
    public void StartFastFormationMove()
    {
        if (currentState == DroneState.Grounded) return;
        currentState = DroneState.FastFormationMove;
    }
    
    // 🆕 NAVİGASYON METHODları
    public void StartNavigationMove(float maxTime)
    {
        if (currentState == DroneState.Grounded) return;
        currentState = DroneState.NavigationMove;
        maxNavigationTime = maxTime;
        navigationMoveTimer = 0f;
    }
    
    public void NavigationLand()
    {
        currentState = DroneState.NavigationLanding;
        targetHeight = 1f;
    }
    
    public void SetAutonomousMode(bool autonomous)
    {
        autonomousMode = autonomous;
        Debug.Log($"🤖 Drone {droneID}: Autonomous mode {(autonomous ? "ENABLED" : "DISABLED")}");
    }
    
    public bool IsAtAssignedPosition(float threshold)
    {
        return Vector3.Distance(transform.position, assignedPosition) < threshold;
    }
    
    public void LockFormation() { currentState = DroneState.FormationHold; }
    
    public void UnlockFormation() { currentState = DroneState.Hovering; }
    
    public void Land()
    {
        currentState = DroneState.Landing;
        targetHeight = 1f;
    }
    
    public Vector3 GetVelocity() { return rb != null ? rb.linearVelocity : Vector3.zero; }
    
    public Vector3 GetTargetPosition()
    {
        switch (currentState)
        {
            case DroneState.Staging: return stagingPosition;
            case DroneState.FormationMove:
            case DroneState.NavigationMove:
            case DroneState.FormationHold: return assignedPosition;
            default: return transform.position;
        }
    }
}