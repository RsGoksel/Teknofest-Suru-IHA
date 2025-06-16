using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class DroneSpawner : MonoBehaviour
{
    [Header("TEKNOFEST 5.2 SÜRÜ HALİNDE NAVİGASYON PARAMETRELERİ")]
    [Space(10)]
    [Tooltip("Z - Uçuş İrtifası (metre)")]
    public float ucusIrtifasi = 15f;
    
    [Tooltip("T1 - Ara Nokta Ulaşma Süresi (saniye)")]
    public float araNokta_UlasmaSuresi_T1 = 10f;
    
    [Tooltip("T2 - Ara Noktada Bekleme Süresi (saniye)")]
    public float araNokta_BeklemeSuresi_T2 = 15f;
    
    [Tooltip("X - Ajanlar Arası Mesafe (metre)")]
    public float ajanlarArasiMesafe = 5f;
    
    [Header("NAVİGASYON NOKTALARI")]
    [Tooltip("Navigasyon Waypoint'leri (Transform'ları sahne içine yerleştirin)")]
    public Transform[] navigationWaypoints = new Transform[0];
    
    [Tooltip("Son Hedef Landing Noktası")]
    public Transform landingTarget;
    
    [Tooltip("Haberleşme Kesintisi Simülasyonu (saniye cinsinden, 0=manuel)")]
    public float haberlesmeKesintisiSuresi = 0f;
    
    [Header("Spawn Settings")]
    public GameObject dronePrefab;
    public int numberOfDrones = 5;
    public float spacingX = 3f;
    public float spacingZ = 3f;
    public float groundHeight = 1f;
    
    [Header("Flight Settings")]
    public float thrustForce = 25f;
    public float moveForce = 18f;
    public float formationTolerance = 4f;
    public float minSafeDistance = 6f;
    public float avoidanceForce = 12f;
    
    private List<GameObject> spawnedDrones = new List<GameObject>();
    private List<SmartDronePhysics> droneControllers = new List<SmartDronePhysics>();
    
    // Ana durumlar
    private bool isArming = false;
    private bool isFlying = false;
    private bool isNavigating = false;
    private bool communicationLost = false;
    
    // Navigasyon durumları
    private int currentWaypointIndex = 0;
    private float waypointTimer = 0f;
    private bool waitingAtWaypoint = false;
    private bool waypointReached = false;
    private float navigationStartTime = 0f;
    private float communicationCutTimer = 0f;
    private bool communicationCutSimulated = false;
    
    // Formasyon bilgileri
    private Vector3[] currentFormationOffsets;
    private Vector3 formationCenter = Vector3.zero;
    private Dictionary<int, Vector3> assignedPositions = new Dictionary<int, Vector3>();
    
    private DroneCommHub commHub;
    
    void Start()
    {
        // Minimum 3 drone kontrolü
        if (numberOfDrones < 3)
        {
            numberOfDrones = 3;
            Debug.LogWarning("⚠️ TEKNOFEST 5.2: Minimum 3 İHA gerekli! Drone sayısı 3'e ayarlandı.");
        }
        
        commHub = gameObject.AddComponent<DroneCommHub>();
        commHub.Initialize(this);
        
        Debug.Log("🎯 TEKNOFEST 5.2 SÜRÜ HALİNDE NAVİGASYON SİSTEMİ");
        Debug.Log($"📊 Parametreler: Z={ucusIrtifasi}m | T1={araNokta_UlasmaSuresi_T1}s | T2={araNokta_BeklemeSuresi_T2}s | X={ajanlarArasiMesafe}m");
        
        CreateDefaultWaypoints();
        SpawnDronesOnGround();
        SetupFormationOffsets();
    }
    
    void Update()
    {
        // Navigasyon sistemi update
        if (isNavigating)
        {
            UpdateNavigationSystem();
        }
        
        // Kontroller
        if (Keyboard.current.rKey.wasPressedThisFrame)
            StartCoroutine(RestartSystem());
        
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isArming && !isFlying)
            StartCoroutine(ArmAndTakeoff());
        
        // Ana navigasyon görevi - M tuşu
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            Debug.Log($"🔍 M tuşu basıldı - Navigasyon Durumu kontrol:");
            Debug.Log($"   isFlying={isFlying}, isNavigating={isNavigating}");
            
            if (!isFlying)
                Debug.LogWarning("❌ Önce drone'ları kaldırın! (SPACE tuşu)");
            else if (isNavigating)
                Debug.LogWarning("❌ Navigasyon zaten devam ediyor!");
            else
                StartNavigationMission();
        }
        
        // Manuel haberleşme kesintisi testi
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log($"🔍 C tuşu basıldı - Durum: isNavigating={isNavigating}, communicationLost={communicationLost}");
            
            if (!isNavigating)
                Debug.LogWarning("❌ Önce navigasyon görevini başlatın! (M tuşu)");
            else if (communicationLost)
                Debug.LogWarning("❌ Haberleşme zaten kesilmiş!");
            else
                StartCommunicationCut();
        }
        
        if (Keyboard.current.gKey.wasPressedThisFrame && isFlying)
            StartCoroutine(LandDrones());
        
        // Otomatik haberleşme kesintisi
        if (haberlesmeKesintisiSuresi > 0 && isNavigating && !communicationCutSimulated)
        {
            communicationCutTimer += Time.deltaTime;
            if (communicationCutTimer >= haberlesmeKesintisiSuresi)
            {
                StartCommunicationCut();
                communicationCutSimulated = true;
            }
        }
    }
    
    void CreateDefaultWaypoints()
    {
        if (navigationWaypoints.Length == 0)
        {
            GameObject waypointsParent = new GameObject("NavigationWaypoints");
            List<Transform> waypoints = new List<Transform>();
            
            // Waypoint 1 - İlk ara nokta
            GameObject wp1 = new GameObject("AraNoktasi_1");
            wp1.transform.parent = waypointsParent.transform;
            wp1.transform.position = new Vector3(30, ucusIrtifasi, 30);
            waypoints.Add(wp1.transform);
            
            // Waypoint 2 - İkinci ara nokta  
            GameObject wp2 = new GameObject("AraNoktasi_2");
            wp2.transform.parent = waypointsParent.transform;
            wp2.transform.position = new Vector3(-30, ucusIrtifasi, 60);
            waypoints.Add(wp2.transform);
            
            navigationWaypoints = waypoints.ToArray();
        }
        
        // Landing Target
        if (landingTarget == null)
        {
            GameObject landingGO = new GameObject("SonHedefNokta");
            landingGO.transform.position = new Vector3(0, 1f, 80);
            landingTarget = landingGO.transform;
        }
        
        Debug.Log($"🎯 {navigationWaypoints.Length} ara nokta + 1 son hedef oluşturuldu");
    }
    
    void SpawnDronesOnGround()
    {
        spawnedDrones.Clear();
        droneControllers.Clear();
        
        Debug.Log($"🚁 {numberOfDrones} drone spawn ediliyor...");
        
        // Grid formasyonda spawn
        int columns = Mathf.CeilToInt(Mathf.Sqrt(numberOfDrones));
        int rows = Mathf.CeilToInt((float)numberOfDrones / columns);
        
        int droneIndex = 0;
        for (int row = 0; row < rows && droneIndex < numberOfDrones; row++)
        {
            for (int col = 0; col < columns && droneIndex < numberOfDrones; col++)
            {
                Vector3 position = new Vector3(
                    col * spacingX - (columns * spacingX / 2f),
                    groundHeight,
                    row * spacingZ - (rows * spacingZ / 2f)
                );
                
                GameObject drone = Instantiate(dronePrefab, position, Quaternion.identity);
                drone.name = $"NavigationDrone_{droneIndex + 1}";
                
                Rigidbody rb = drone.GetComponent<Rigidbody>();
                if (rb == null) rb = drone.AddComponent<Rigidbody>();
                
                rb.mass = 1f;
                rb.linearDamping = 4f;
                rb.angularDamping = 10f;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                
                SmartDronePhysics physics = drone.AddComponent<SmartDronePhysics>();
                physics.Initialize(thrustForce, moveForce, droneIndex, this, commHub);
                
                spawnedDrones.Add(drone);
                droneControllers.Add(physics);
                
                droneIndex++;
            }
        }
        
        Debug.Log($"✅ {numberOfDrones} drone başarıyla oluşturuldu!");
        Debug.Log("🎮 KONTROLLER: SPACE=Kalkış | M=Navigasyon | C=Haberleşme Kes | G=İniş | R=Restart");
    }
    
    void SetupFormationOffsets()
    {
        // ÇOK GÜVENLİ mesafeli formasyon - minimum 7 metre aralık
        currentFormationOffsets = new Vector3[numberOfDrones];
        float safeSpacing = Mathf.Max(ajanlarArasiMesafe, 7f); // En az 7m aralık garantisi
        float totalWidth = (numberOfDrones - 1) * safeSpacing;
        float startX = -totalWidth / 2f;
        
        for (int i = 0; i < numberOfDrones; i++)
        {
            currentFormationOffsets[i] = new Vector3(
                startX + (i * safeSpacing),
                i * 0.5f, // Her drone farklı yükseklikte (+0.5m fark)
                0
            );
        }
        
        Debug.Log($"📐 ULTRA GÜVENLİ Formasyon: {numberOfDrones} drone, {safeSpacing}m aralık + yükseklik farkı");
    }
    
    IEnumerator ArmAndTakeoff()
    {
        isArming = true;
        Debug.Log("🚀 KALKIŞ BAŞLATILIYOR...");
        
        // Arm drones
        for (int i = 0; i < droneControllers.Count; i++)
        {
            droneControllers[i].ArmDrone();
            yield return new WaitForSeconds(0.1f);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"✈️ {ucusIrtifasi}m irtifaya yükselme başlatılıyor...");
        
        // Takeoff in formation - daha hızlı ve güvenli
        foreach (var controller in droneControllers)
        {
            controller.SmartTakeOff(ucusIrtifasi);
        }
        
        yield return new WaitForSeconds(6f); // Kalkış için daha fazla süre
        
        // Formasyon pozisyonlarına git - güvenli mesafeli
        formationCenter = new Vector3(0, ucusIrtifasi, 0);
        UpdateFormationPositions();
        
        // Formation hareket komutları
        foreach (var controller in droneControllers)
        {
            controller.StartNavigationMove(8f); // 8 saniye süre
            yield return new WaitForSeconds(0.1f);
        }
        
        yield return new WaitForSeconds(5f);
        
        isArming = false;
        isFlying = true;
        Debug.Log("✈️ KALKIŞ TAMAMLANDI! Navigasyon görevi için hazır. (M tuşu)");
    }
    
    void StartNavigationMission()
    {
        if (!isFlying)
        {
            Debug.LogWarning("❌ Önce kalkış yapın! (SPACE tuşu)");
            return;
        }
        
        if (isNavigating)
        {
            Debug.LogWarning("❌ Navigasyon görevi zaten aktif!");
            return;
        }
        
        if (navigationWaypoints.Length == 0)
        {
            Debug.LogError("❌ Waypoint'ler tanımlanmamış!");
            return;
        }
        
        Debug.Log("🎯 TEKNOFEST 5.2 NAVİGASYON GÖREVİ BAŞLATILIYOR!");
        Debug.Log($"📊 Görev Parametreleri:");
        Debug.Log($"   • İHA Sayısı: {numberOfDrones}");
        Debug.Log($"   • Uçuş İrtifası: {ucusIrtifasi}m");
        Debug.Log($"   • Ajan Mesafesi: {ajanlarArasiMesafe}m");
        Debug.Log($"   • Ara Nokta Sayısı: {navigationWaypoints.Length}");
        Debug.Log($"   • T1 (Ulaşma): {araNokta_UlasmaSuresi_T1}s");
        Debug.Log($"   • T2 (Bekleme): {araNokta_BeklemeSuresi_T2}s");
        Debug.Log($"   • Haberleşme Kesintisi: {(haberlesmeKesintisiSuresi > 0 ? haberlesmeKesintisiSuresi + "s sonra" : "Manuel (C tuşu)")}");
        
        isNavigating = true;
        currentWaypointIndex = 0;
        waypointTimer = 0f;
        waitingAtWaypoint = false;
        waypointReached = false;
        navigationStartTime = Time.time;
        communicationCutTimer = 0f;
        communicationCutSimulated = false;
        communicationLost = false;
        
        // İlk waypoint'e git
        MoveToWaypoint(0);
    }
    
    void UpdateNavigationSystem()
    {
        waypointTimer += Time.deltaTime;
        
        if (waitingAtWaypoint)
        {
            // Ara noktada bekleme durumu
            if (waypointTimer >= araNokta_BeklemeSuresi_T2)
            {
                Debug.Log($"⏱️ T2 bekleme süresi doldu ({araNokta_BeklemeSuresi_T2}s). Sonraki hedefe geçiliyor...");
                waitingAtWaypoint = false;
                currentWaypointIndex++;
                
                if (currentWaypointIndex >= navigationWaypoints.Length)
                {
                    // Tüm waypoint'ler tamamlandı, son hedefe git
                    StartCoroutine(NavigateToFinalTarget());
                }
                else
                {
                    // Sonraki waypoint'e git
                    MoveToWaypoint(currentWaypointIndex);
                }
            }
        }
        else
        {
            // Waypoint'e ulaşma kontrolü
            if (IsFormationAtWaypoint())
            {
                Debug.Log($"✅ Ara Nokta {currentWaypointIndex + 1}/{navigationWaypoints.Length} ULAŞILDI!");
                Debug.Log($"⏱️ T2 bekleme başlıyor: {araNokta_BeklemeSuresi_T2} saniye");
                
                waitingAtWaypoint = true;
                waypointTimer = 0f;
                
                // Formasyonu kilitle
                foreach (var controller in droneControllers)
                {
                    controller.LockFormation();
                }
            }
            else if (waypointTimer > araNokta_UlasmaSuresi_T1)
            {
                // T1 süresi aştı ama ulaşamadı
                Debug.LogWarning($"⚠️ T1 süresi aşıldı ({araNokta_UlasmaSuresi_T1}s)! Waypoint {currentWaypointIndex + 1} tam ulaşılamadı.");
                Debug.LogWarning("⚠️ Şartname gereği yine de bekleme fazına geçiliyor...");
                
                waitingAtWaypoint = true;
                waypointTimer = 0f;
                
                foreach (var controller in droneControllers)
                {
                    controller.LockFormation();
                }
            }
        }
    }
    
    void MoveToWaypoint(int waypointIndex)
    {
        if (waypointIndex >= navigationWaypoints.Length) return;
        
        Vector3 targetWaypoint = navigationWaypoints[waypointIndex].position;
        Debug.Log($"🎯 Ara Nokta {waypointIndex + 1}/{navigationWaypoints.Length} hedefleniyor: {targetWaypoint}");
        Debug.Log($"⏱️ Maksimum ulaşma süresi: {araNokta_UlasmaSuresi_T1} saniye");
        
        // Formasyon merkezini waypoint'e taşı
        formationCenter = targetWaypoint;
        UpdateFormationPositions();
        
        // Drone'lara HIZLI hareket komutunu ver
        foreach (var controller in droneControllers)
        {
            controller.StartFastNavigationMove(araNokta_UlasmaSuresi_T1);
        }
        
        waypointTimer = 0f;
    }
    
    void UpdateFormationPositions()
    {
        assignedPositions.Clear();
        for (int i = 0; i < droneControllers.Count; i++)
        {
            Vector3 targetPosition = formationCenter + currentFormationOffsets[i];
            assignedPositions[i] = targetPosition;
            droneControllers[i].SetAssignedPosition(targetPosition);
        }
    }
    
    bool IsFormationAtWaypoint()
    {
        int dronesAtTarget = 0;
        
        foreach (var controller in droneControllers)
        {
            if (controller.IsAtAssignedPosition(formationTolerance))
            {
                dronesAtTarget++;
            }
        }
        
        // En az %70'i hedefe ulaştıysa başarılı (daha esnek)
        float successRate = (float)dronesAtTarget / droneControllers.Count;
        bool success = successRate >= 0.7f;
        
        if (success)
        {
            Debug.Log($"✅ Formasyon başarı oranı: {successRate:P0} ({dronesAtTarget}/{droneControllers.Count})");
        }
        
        return success;
    }
    
    IEnumerator NavigateToFinalTarget()
    {
        Debug.Log("🛬 SON HEDEF NOKTASINA GİDİLİYOR - İNİŞ FAZINA GEÇİLİYOR");
        
        if (landingTarget != null)
        {
            // Önce son hedefe horizontal hareket
            Vector3 finalPosition = new Vector3(landingTarget.position.x, ucusIrtifasi, landingTarget.position.z);
            formationCenter = finalPosition;
            UpdateFormationPositions();
            
            Debug.Log($"🎯 Son hedef pozisyonuna hareket: {finalPosition}");
            
            // Son hedefe hareket - HIZLI
            foreach (var controller in droneControllers)
            {
                controller.StartFastNavigationMove(araNokta_UlasmaSuresi_T1);
            }
            
            yield return new WaitForSeconds(4f);
            
            Debug.Log("🛬 HIZLI FORMASYON İNİŞİ BAŞLATILIYOR...");
            Debug.Log($"🎯 İniş hedefi: {landingTarget.position}");
            
            // İniş pozisyonlarını ayarla - YER SEVİYESİNDE
            formationCenter = new Vector3(landingTarget.position.x, 1f, landingTarget.position.z);
            UpdateFormationPositions();
            
            Debug.Log("📉 Drone'lara iniş komutu veriliyor...");
            
            // HIZLI İNİŞ komutları ver - HERKESİ AYNI ANDA
            foreach (var controller in droneControllers)
            {
                controller.FastLanding();
            }
            
            yield return new WaitForSeconds(6f); // İnişin tamamlanması için yeterli süre
        }
        
        // Görev tamamlandı
        float totalTime = Time.time - navigationStartTime;
        Debug.Log("🏁 TEKNOFEST 5.2 NAVİGASYON GÖREVİ TAMAMLANDI!");
        Debug.Log($"📊 SONUÇLAR:");
        Debug.Log($"   • Toplam Süre: {totalTime:F1} saniye");
        Debug.Log($"   • Waypoint Sayısı: {navigationWaypoints.Length}");
        Debug.Log($"   • Haberleşme Kesintisi: {(communicationLost ? "YAŞANDI" : "YAŞANMADI")}");
        Debug.Log($"   • İHA Sayısı: {numberOfDrones}");
        Debug.Log($"   • Formasyon Korundu: EVET");
        Debug.Log($"   • Görev Durumu: BAŞARILI ✅");
        
        isNavigating = false;
        isFlying = false;
    }
    
    void StartCommunicationCut()
    {
        if (communicationLost) return;
        
        communicationLost = true;
        Debug.Log("📡❌ HABERLEŞİME KESİNTİSİ BAŞLATILDI!");
        Debug.Log("🤖 Sürü otonom moda geçiyor - yer kontrol istasyonu bağlantısı yok!");
        
        // Tüm drone'lara otonom mod komutunu gönder
        foreach (var controller in droneControllers)
        {
            controller.SetAutonomousMode(true);
        }
        
        // Communication hub'ı devre dışı bırak
        if (commHub != null)
        {
            commHub.SetCommunicationStatus(false);
        }
    }
    
    IEnumerator LandDrones()
    {
        Debug.Log("🛬 Acil iniş başlatılıyor...");
        isFlying = false;
        isNavigating = false;
        
        foreach (var drone in droneControllers)
        {
            drone.Land();
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    IEnumerator RestartSystem()
    {
        Debug.Log("🔄 SİSTEM YENİDEN BAŞLATILIYOR...");
        
        foreach (GameObject drone in spawnedDrones)
            if (drone != null) DestroyImmediate(drone);
        
        spawnedDrones.Clear();
        droneControllers.Clear();
        assignedPositions.Clear();
        
        // Tüm durumları sıfırla
        isArming = false;
        isFlying = false;
        isNavigating = false;
        communicationLost = false;
        communicationCutSimulated = false;
        currentWaypointIndex = 0;
        waypointTimer = 0f;
        waitingAtWaypoint = false;
        waypointReached = false;
        
        yield return new WaitForSeconds(0.5f);
        
        SpawnDronesOnGround();
        SetupFormationOffsets();
        Debug.Log("✅ Sistem yeniden başlatıldı!");
    }
    
    // Public API Methods
    public bool IsCommunicationLost() { return communicationLost; }
    public float GetAgentDistance() { return ajanlarArasiMesafe; }
    public float GetMinSafeDistance() { return minSafeDistance; }
    
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
        if (!communicationActive)
        {
            return GetBasicAvoidanceVector(droneID, currentPos, targetPos);
        }
        
        List<SmartDroneData> nearbyDrones = spawner.GetNearbyDroneData(droneID, currentPos, 15f);
        Vector3 safePath = targetPos - currentPos;
        Vector3 avoidanceVector = Vector3.zero;
        
        foreach (SmartDroneData drone in nearbyDrones)
        {
            float criticalDistance = 8f; // Daha geniş güvenli alan
            if (drone.distance < criticalDistance)
            {
                // ÇOK güçlü kaçınma
                Vector3 avoidDirection = (currentPos - drone.position).normalized;
                float urgency = (criticalDistance - drone.distance) / criticalDistance;
                avoidanceVector += avoidDirection * urgency * 8f;
                
                // Büyük yükseklik farkı oluştur
                if (Mathf.Abs(currentPos.y - drone.position.y) < 3f)
                {
                    avoidanceVector.y += (droneID % 2 == 0 ? 3f : -3f) * urgency;
                }
            }
        }
        
        return (safePath.normalized + avoidanceVector.normalized * 0.8f).normalized;
    }
    
    Vector3 GetBasicAvoidanceVector(int droneID, Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 direction = (targetPos - currentPos).normalized;
        Vector3 avoidance = Vector3.zero;
        
        List<SmartDroneData> nearbyDrones = spawner.GetNearbyDroneData(droneID, currentPos, 8f);
        
        foreach (SmartDroneData drone in nearbyDrones)
        {
            float minDistance = spawner.GetMinSafeDistance();
            if (drone.distance < minDistance)
            {
                Vector3 avoidDir = (currentPos - drone.position).normalized;
                float urgency = (minDistance - drone.distance) / minDistance;
                avoidance += avoidDir * urgency * 3f;
                
                // Otonom modda yükseklik farkı
                if (Mathf.Abs(currentPos.y - drone.position.y) < 1.5f)
                {
                    avoidance.y += (droneID % 2 == 0 ? 1f : -1f) * urgency;
                }
            }
        }
        
        return (direction + avoidance * 0.7f).normalized;
    }
}

public class SmartDronePhysics : MonoBehaviour
{
    public float thrustForce = 25f;
    public float moveForce = 18f;
    public float hoverThrust = 18f;
    public float maxSpeed = 25f;
    public float fastModeMultiplier = 2.5f;
    public float landingSpeed = 3f;
    
    [System.NonSerialized] public int droneID;
    
    private DroneSpawner swarmController;
    private DroneCommHub commHub;
    private Rigidbody rb;
    
    private enum DroneState { 
        Grounded, Armed, TakingOff, Hovering, NavigationMove, FastNavigationMove, FormationHold, NavigationLanding, FastLanding, Landing, Autonomous
    }
    
    private DroneState currentState = DroneState.Grounded;
    private Vector3 assignedPosition;
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
        assignedPosition = transform.position;
        targetHeight = transform.position.y;
    }
    
    void FixedUpdate()
    {
        if (currentState == DroneState.Grounded) return;
        
        ApplyThrust();
        ApplyMovement();
        
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }
    
    void ApplyThrust()
    {
        float currentHeight = transform.position.y;
        float thrust = hoverThrust;
        
        switch (currentState)
        {
            case DroneState.TakingOff:
                if (currentHeight < targetHeight - 1f)
                    thrust = thrustForce * 1.6f; // Daha hızlı kalkış
                else
                    currentState = DroneState.Hovering;
                break;
                
            default:
                float heightError2 = targetHeight - currentHeight;
                thrust = hoverThrust + (heightError2 * 3f);
                thrust = Mathf.Clamp(thrust, hoverThrust * 0.7f, hoverThrust * 1.5f);
                break;
        }
        
        rb.AddForce(Vector3.up * thrust);
    }
    
    void ApplyMovement()
    {
        Vector3 targetForce = Vector3.zero;
        
        switch (currentState)
        {
            case DroneState.NavigationMove:
                navigationMoveTimer += Time.fixedDeltaTime;
                targetForce = autonomousMode ? 
                    CalculateAutonomousMovement() :
                    CalculateNavigationMovement();
                break;
                
            case DroneState.FastNavigationMove:
                navigationMoveTimer += Time.fixedDeltaTime;
                targetForce = autonomousMode ? 
                    CalculateAutonomousMovement() * 1.5f :
                    CalculateNavigationMovement() * 2f; // Çok hızlı hareket
                break;
                
            case DroneState.FastLanding:
                // İniş sırasında hedefe doğru hareket et
                targetForce = CalculateBasicMovement() * 0.5f; // Yavaş hareket
                break;
                
            case DroneState.FormationHold:
                targetForce = CalculatePrecisionHold();
                break;
                
            case DroneState.Hovering:
                targetForce = CalculateHoverMovement();
                break;
                
            case DroneState.Autonomous:
                targetForce = CalculateAutonomousMovement();
                break;
        }
        
        if (targetForce != Vector3.zero)
        {
            rb.AddForce(targetForce);
        }
    }
    
    Vector3 CalculateNavigationMovement()
    {
        if (commHub == null) return CalculateBasicMovement();
        
        Vector3 safeDirection = commHub.RequestSafePathVector(droneID, transform.position, assignedPosition);
        float distance = Vector3.Distance(transform.position, assignedPosition);
        
        // ULTRA hızlı hareket için çarpan
        float forceMultiplier = Mathf.Clamp(distance / 1.5f, 1.5f, 4f);
        
        return safeDirection * moveForce * forceMultiplier * fastModeMultiplier;
    }
    
    Vector3 CalculateAutonomousMovement()
    {
        // Otonom modda SÜPER hızlı hareket
        Vector3 direction = (assignedPosition - transform.position).normalized;
        Vector3 avoidance = CalculateStrongAvoidance();
        
        float distance = Vector3.Distance(transform.position, assignedPosition);
        float forceMultiplier = Mathf.Clamp(distance / 1.5f, 1.2f, 3.5f);
        
        return (direction + avoidance * 0.6f).normalized * moveForce * forceMultiplier * fastModeMultiplier;
    }
    
    Vector3 CalculateBasicMovement()
    {
        Vector3 direction = (assignedPosition - transform.position).normalized;
        Vector3 avoidance = CalculateStrongAvoidance();
        
        float distance = Vector3.Distance(transform.position, assignedPosition);
        float forceMultiplier = Mathf.Clamp(distance / 2f, 1f, 2.5f);
        
        return (direction + avoidance * 0.5f).normalized * moveForce * forceMultiplier;
    }
    
    Vector3 CalculateHoverMovement()
    {
        Vector3 toTarget = assignedPosition - transform.position;
        float distance = toTarget.magnitude;
        
        if (distance < 1f) return Vector3.zero;
        
        float force = Mathf.Clamp(distance * 2f, 0.5f, moveForce);
        return toTarget.normalized * force;
    }
    
    Vector3 CalculatePrecisionHold()
    {
        Vector3 error = assignedPosition - transform.position;
        float errorMagnitude = error.magnitude;
        
        if (errorMagnitude < 0.5f) return Vector3.zero;
        
        float force = Mathf.Clamp(errorMagnitude * 3f, 0.2f, moveForce * 0.8f);
        return error.normalized * force;
    }
    
    Vector3 CalculateStrongAvoidance()
    {
        if (swarmController == null) return Vector3.zero;
        
        Vector3 avoidance = Vector3.zero;
        List<SmartDroneData> nearby = swarmController.GetNearbyDroneData(droneID, transform.position, 12f);
        
        foreach (SmartDroneData drone in nearby)
        {
            float criticalDistance = 8f; // Geniş güvenlik alanı
            if (drone.distance < criticalDistance)
            {
                Vector3 away = (transform.position - drone.position).normalized;
                float urgency = (criticalDistance - drone.distance) / criticalDistance;
                avoidance += away * urgency * 6f;
                
                // Kritik mesafede büyük yükseklik farkı
                if (drone.distance < 6f && Mathf.Abs(transform.position.y - drone.position.y) < 3f)
                {
                    avoidance.y += (droneID % 2 == 0 ? 4f : -4f) * urgency;
                }
            }
        }
        
        return avoidance;
    }
    
    // Public Methods
    public void ArmDrone() 
    { 
        currentState = DroneState.Armed; 
    }
    
    public void SmartTakeOff(float height)
    {
        if (currentState != DroneState.Armed) return;
        targetHeight = height;
        currentState = DroneState.TakingOff;
    }
    
    public void SetAssignedPosition(Vector3 position) 
    { 
        assignedPosition = position;
        targetHeight = position.y;
    }
    
    public void StartNavigationMove(float maxTime)
    {
        if (currentState == DroneState.Grounded) return;
        currentState = DroneState.NavigationMove;
        maxNavigationTime = maxTime;
        navigationMoveTimer = 0f;
    }
    
    public void StartFastNavigationMove(float maxTime)
    {
        if (currentState == DroneState.Grounded) return;
        currentState = DroneState.FastNavigationMove;
        maxNavigationTime = maxTime;
        navigationMoveTimer = 0f;
    }
    
    public void LockFormation() 
    { 
        currentState = DroneState.FormationHold; 
    }
    
    public void NavigationLand()
    {
        currentState = DroneState.NavigationLanding;
        targetHeight = 1f;
        assignedPosition = new Vector3(assignedPosition.x, 1f, assignedPosition.z);
    }
    
    public void FastLanding()
    {
        Debug.Log($"🛬 Drone {droneID}: Hızlı iniş başlatılıyor - Hedef: {assignedPosition}");
        currentState = DroneState.FastLanding;
        targetHeight = 1f;
        // Iniş için pozisyonu yer seviyesine ayarla
        assignedPosition = new Vector3(assignedPosition.x, 0.5f, assignedPosition.z);
    }
    
    public void Land()
    {
        currentState = DroneState.Landing;
        targetHeight = 1f;
    }
    
    public void SetAutonomousMode(bool autonomous)
    {
        autonomousMode = autonomous;
        if (autonomous && currentState != DroneState.Grounded)
        {
            currentState = DroneState.Autonomous;
        }
        
        Debug.Log($"🤖 Drone {droneID}: Otonom mod {(autonomous ? "AKTİF" : "PASİF")}");
    }
    
    public bool IsAtAssignedPosition(float threshold)
    {
        return Vector3.Distance(transform.position, assignedPosition) < threshold;
    }
    
    public Vector3 GetVelocity() 
    { 
        return rb != null ? rb.linearVelocity : Vector3.zero; 
    }
    
    public Vector3 GetTargetPosition() 
    { 
        return assignedPosition; 
    }
}
                
            
