// =================================================================
// MERKEZİ İLETİŞİM HUB'I VE KOORDİNASYON SİSTEMİ
// =================================================================
// AMAÇ: Sürü İHA'larının merkezi zeka ile koordinasyonu
//       Gerçek zamanlı bilgi paylaşımı ve akıllı karar verme
// =================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İHA VERİ YAPISI
/// Her İHA'nın anlık durumu için veri container'ı
/// </summary>
[System.Serializable]
public class DroneData
{
    public int id;                    // İHA kimlik numarası
    public Vector3 position;          // Anlık pozisyon
    public Vector3 velocity;          // Anlık hız vektörü
    public Vector3 targetPosition;    // Hedef pozisyon
    public float distance;            // Referans noktasına uzaklık
    public DroneState currentState;   // İHA durumu
    public float batteryLevel;        // Batarya seviyesi (gelecek için)
    public float signalStrength;      // Sinyal gücü (gelecek için)
}

/// <summary>
/// İHA DURUM MAKINESI
/// Finite State Machine (FSM) yaklaşımı
/// </summary>
public enum DroneState
{
    Grounded,           // Yerde
    Armed,              // Silahlandırılmış (Hazır)
    TakingOff,          // Kalkış yapıyor
    Hovering,           // Sabit uçuş
    Staging,            // Hazırlık pozisyonuna gidiyor
    FormationMove,      // Formasyon pozisyonuna gidiyor
    FormationHold,      // Formasyon pozisyonunu koruyor
    Landing             // İniş yapıyor
}

/// <summary>
/// MERKEZİ İLETİŞİM HUB'I
/// Sürü İHA'larının merkezi koordinasyon sistemi
/// 
/// Tasarım Prensibi: Command and Control (C2) Architecture
/// - Merkezi karar verme
/// - Dağıtık uygulama  
/// - Gerçek zamanlı bilgi paylaşımı
/// </summary>
public class CentralCommunicationHub : MonoBehaviour
{
    [Header("İletişim Parametreleri")]
    public float communicationRange = 20f;      // İletişim menzili
    public float updateFrequency = 50f;         // Güncelleme frekansı (Hz)
    public int maxConcurrentDrones = 50;        // Maksimum eş zamanlı İHA
    
    // VERİ YAPILARI
    private Dictionary<int, DroneData> droneDatabase;     // İHA veritabanı
    private Queue<DroneCommand> commandQueue;             // Komut kuyruğu
    private List<Vector3> reservedPositions;              // Rezerve edilmiş pozisyonlar
    private DroneSpawner swarmController;                 // Sürü kontrolcüsü
    
    // PERFORMANS METRİKLERİ
    private float lastUpdateTime;
    private int processedCommandsPerSecond;
    private int activeConnections;
    
    void Awake()
    {
        InitializeHub();
    }
    
    /// <summary>
    /// HUB'I BAŞLATMA
    /// Veri yapılarını ve sistemleri hazırlama
    /// </summary>
    void InitializeHub()
    {
        droneDatabase = new Dictionary<int, DroneData>();
        commandQueue = new Queue<DroneCommand>();
        reservedPositions = new List<Vector3>();
        
        // Güncelleme döngüsünü başlat
        InvokeRepeating(nameof(ProcessCommunications), 0f, 1f / updateFrequency);
        
        Debug.Log($"🛰️ Merkezi İletişim Hub'ı başlatıldı - Frekans: {updateFrequency}Hz");
    }
    
    /// <summary>
    /// İHA KAYIT SİSTEMİ
    /// Yeni İHA'ları sisteme kaydetme
    /// </summary>
    public bool RegisterDrone(int droneId, Vector3 initialPosition)
    {
        if (droneDatabase.ContainsKey(droneId))
        {
            Debug.LogWarning($"⚠️ İHA {droneId} zaten kayıtlı!");
            return false;
        }
        
        if (droneDatabase.Count >= maxConcurrentDrones)
        {
            Debug.LogError($"❌ Maksimum İHA sayısına ulaşıldı: {maxConcurrentDrones}");
            return false;
        }
        
        // Yeni İHA verisi oluştur
        DroneData newDrone = new DroneData
        {
            id = droneId,
            position = initialPosition,
            velocity = Vector3.zero,
            targetPosition = initialPosition,
            distance = 0f,
            currentState = DroneState.Grounded,
            batteryLevel = 100f,
            signalStrength = 100f
        };
        
        droneDatabase[droneId] = newDrone;
        activeConnections++;
        
        Debug.Log($"✅ İHA {droneId} başarıyla kayıt edildi. Aktif bağlantı: {activeConnections}");
        return true;
    }
    
    /// <summary>
    /// GÜVENLİ YOL PLANLAMA ALGORİTMASI
    /// Matematiksel Model: Potansiyel Alan Yöntemi (Potential Field Method)
    /// 
    /// F_total = F_attractive + F_repulsive
    /// F_attractive = K_att * (P_target - P_current)
    /// F_repulsive = Σ K_rep * (1/d_i - 1/d_safe) * (P_current - P_obstacle_i)
    /// </summary>
    public Vector3 CalculateSafePathVector(int droneId, Vector3 currentPos, Vector3 targetPos)
    {
        if (!droneDatabase.ContainsKey(droneId))
        {
            Debug.LogError($"❌ İHA {droneId} veritabanında bulunamadı!");
            return (targetPos - currentPos).normalized;
        }
        
        // 1. HEDEFİN ÇEKİM KUVVETİ (Attractive Force)
        Vector3 attractiveForce = (targetPos - currentPos).normalized;
        
        // 2. YAKINDAKI İHA'LARI SORGULA
        List<DroneData> nearbyDrones = GetNearbyDrones(droneId, currentPos, communicationRange);
        
        // 3. İTME KUVVETİ HESAPLAMA (Repulsive Force)
        Vector3 repulsiveForce = Vector3.zero;
        
        foreach (DroneData nearbyDrone in nearbyDrones)
        {
            float distance = nearbyDrone.distance;
            
            if (distance < 5f && distance > 0.1f)
            {
                // Gelecek pozisyon tahmini (Predictive Algorithm)
                Vector3 predictedPos = nearbyDrone.position + nearbyDrone.velocity * 2f;
                
                // İtme yönü
                Vector3 repulsiveDirection = (currentPos - predictedPos).normalized;
                
                // Kuvvet büyüklüğü (Ters kare yasası)
                float repulsiveMagnitude = (5f - distance) / 5f;
                
                repulsiveForce += repulsiveDirection * repulsiveMagnitude * 3f;
            }
        }
        
        // 4. TOPLAM GÜVENLİ YOL VEKTÖRÜ
        Vector3 safePathVector = (attractiveForce + repulsiveForce.normalized).normalized;
        
        // DEBUG: Kritik durumlar
        if (nearbyDrones.Count > 3)
        {
            Debug.LogWarning($"🚨 İHA {droneId} yoğun bölgede: {nearbyDrones.Count} komşu");
        }
        
        return safePathVector;
    }
    
    /// <summary>
    /// YAKINDAKI İHA'LARI SORGULA
    /// Spatial Hashing algoritması ile optimize edilmiş komşu arama
    /// </summary>
    public List<DroneData> GetNearbyDrones(int excludeId, Vector3 position, float range)
    {
        List<DroneData> nearbyDrones = new List<DroneData>();
        
        foreach (var droneEntry in droneDatabase)
        {
            if (droneEntry.Key != excludeId)
            {
                DroneData drone = droneEntry.Value;
                float distance = Vector3.Distance(position, drone.position);
                
                if (distance <= range)
                {
                    // Mesafe bilgisini güncelle
                    drone.distance = distance;
                    nearbyDrones.Add(drone);
                }
            }
        }
        
        // Mesafeye göre sırala (en yakın önce)
        nearbyDrones.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        return nearbyDrones;
    }
    
    /// <summary>
    /// İHA DURUM GÜNCELLEME
    /// Gerçek zamanlı İHA verilerini güncelleme
    /// </summary>
    public void UpdateDroneStatus(int droneId, Vector3 position, Vector3 velocity, 
                                DroneState state, Vector3 targetPos)
    {
        if (!droneDatabase.ContainsKey(droneId))
        {
            Debug.LogError($"❌ İHA {droneId} güncellenemiyor - kayıtlı değil!");
            return;
        }
        
        DroneData drone = droneDatabase[droneId];
        drone.position = position;
        drone.velocity = velocity;
        drone.currentState = state;
        drone.targetPosition = targetPos;
        
        // Sinyal gücü simülasyonu (mesafe bazlı)
        float distanceToHub = Vector3.Distance(position, transform.position);
        drone.signalStrength = Mathf.Clamp(100f - (distanceToHub / communicationRange) * 100f, 0f, 100f);
    }
    
    /// <summary>
    /// İLETİŞİM İŞLEME DÖNGÜSÜ
    /// Ana koordinasyon algoritması - her frame çalışır
    /// </summary>
    void ProcessCommunications()
    {
        // 1. KOMUT KUYRUĞUNU İŞLE
        ProcessCommandQueue();
        
        // 2. İHA DURUMLARINI KONTROL ET
        CheckDroneStatuses();
        
        // 3. ÇARPIŞMA TEHLİKESİ ANALİZİ
        AnalyzeCollisionRisks();
        
        // 4. PERFORMANS METRİKLERİNİ GÜNCELLE
        UpdatePerformanceMetrics();
    }
    
    /// <summary>
    /// ÇARPIŞMA TEHLİKESİ ANALİZİ
    /// Proaktif çarpışma önleme sistemi
    /// </summary>
    void AnalyzeCollisionRisks()
    {
        List<int> highRiskDrones = new List<int>();
        
        foreach (var droneEntry in droneDatabase)
        {
            DroneData drone = droneEntry.Value;
            
            // Aktif İHA'lar için risk analizi
            if (drone.currentState == DroneState.FormationMove || 
                drone.currentState == DroneState.Staging)
            {
                List<DroneData> nearbyDrones = GetNearbyDrones(drone.id, drone.position, 3f);
                
                if (nearbyDrones.Count > 0)
                {
                    // En yakın İHA'ya mesafe
                    float minDistance = nearbyDrones[0].distance;
                    
                    if (minDistance < 2f)
                    {
                        highRiskDrones.Add(drone.id);
                    }
                }
            }
        }
        
        // Yüksek riskli İHA'lar için acil önlem
        if (highRiskDrones.Count > 0)
        {
            Debug.LogWarning($"⚠️ Çarpışma riski: {string.Join(", ", highRiskDrones)} numaralı İHA'lar");
            
            // Acil durum protokolü tetikle
            foreach (int droneId in highRiskDrones)
            {
                SendEmergencyCommand(droneId, "COLLISION_AVOIDANCE");
            }
        }
    }
    
    /// <summary>
    /// ACİL DURUM KOMUTU GÖNDERME
    /// Kritik durumlarda otomatik müdahale
    /// </summary>
    void SendEmergencyCommand(int droneId, string commandType)
    {
        DroneCommand emergencyCommand = new DroneCommand
        {
            droneId = droneId,
            commandType = commandType,
            priority = CommandPriority.Emergency,
            timestamp = Time.time,
            parameters = new Dictionary<string, object>
            {
                ["action"] = "hover_and_wait",
                ["duration"] = 3f
            }
        };
        
        // Acil komutları kuyruğun başına ekle
        var tempQueue = new Queue<DroneCommand>();
        tempQueue.Enqueue(emergencyCommand);
        
        while (commandQueue.Count > 0)
        {
            tempQueue.Enqueue(commandQueue.Dequeue());
        }
        
        commandQueue = tempQueue;
        
        Debug.Log($"🚨 İHA {droneId} için acil durum komutu gönderildi: {commandType}");
    }
    
    /// <summary>
    /// PERFORMANS METRİKLERİ
    /// Sistem performansını izleme
    /// </summary>
    void UpdatePerformanceMetrics()
    {
        if (Time.time - lastUpdateTime >= 1f)
        {
            Debug.Log($"📊 Hub Performansı - Aktif İHA: {activeConnections}, " +
                     $"Komut/sn: {processedCommandsPerSecond}, " +
                     $"Sinyal Gücü Ort: {CalculateAverageSignalStrength():F1}%");
            
            processedCommandsPerSecond = 0;
            lastUpdateTime = Time.time;
        }
    }
    
    /// <summary>
    /// ORTALAMA SİNYAL GÜCÜ HESAPLAMA
    /// Ağ sağlığı göstergesi
    /// </summary>
    float CalculateAverageSignalStrength()
    {
        if (droneDatabase.Count == 0) return 0f;
        
        float totalSignal = 0f;
        foreach (var drone in droneDatabase.Values)
        {
            totalSignal += drone.signalStrength;
        }
        
        return totalSignal / droneDatabase.Count;
    }
    
    // YARDIMCI SINIFLAR
    [System.Serializable]
    public class DroneCommand
    {
        public int droneId;
        public string commandType;
        public CommandPriority priority;
        public float timestamp;
        public Dictionary<string, object> parameters;
    }
    
    public enum CommandPriority
    {
        Low,        // Düşük öncelik
        Normal,     // Normal öncelik  
        High,       // Yüksek öncelik
        Emergency   // Acil durum
    }
    
    void ProcessCommandQueue()
    {
        while (commandQueue.Count > 0)
        {
            DroneCommand command = commandQueue.Dequeue();
            processedCommandsPerSecond++;
            
            // Komut işleme mantığı burada olacak
            Debug.Log($"⚡ Komut işlendi: İHA {command.droneId} - {command.commandType}");
        }
    }
    
    void CheckDroneStatuses()
    {
        // İHA durumlarını kontrol et
        foreach (var drone in droneDatabase.Values)
        {
            if (drone.signalStrength < 20f)
            {
                Debug.LogWarning($"📡 İHA {drone.id} zayıf sinyal: {drone.signalStrength:F1}%");
            }
        }
    }
}