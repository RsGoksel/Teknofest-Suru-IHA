using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TEKNOFEST Sürü İHA - Veri Yapıları ve Enums
/// Tüm sistem genelinde kullanılan data structures
/// </summary>

/// <summary>
/// İHA Veri Yapısı - Her drone'un anlık durumu
/// </summary>
[System.Serializable]
public class SmartDroneData
{
    [Header("Identity")]
    public int id;                      // Drone kimlik numarası
    
    [Header("Position & Movement")]
    public Vector3 position;            // Anlık 3D pozisyon
    public Vector3 velocity;            // Hız vektörü
    public Vector3 targetPosition;      // Hedef pozisyon
    
    [Header("Distance & Metrics")]
    public float distance;              // Referans noktasına uzaklık
    public float batteryLevel = 100f;   // Batarya seviyesi (%)
    public float signalStrength = 100f; // Sinyal gücü (%)
    
    [Header("Status")]
    public DroneStatusEnum status;      // Drone durumu
    public float lastUpdateTime;        // Son güncelleme zamanı
    
    /// <summary>
    /// Drone verisi constructor
    /// </summary>
    public SmartDroneData()
    {
        id = -1;
        position = Vector3.zero;
        velocity = Vector3.zero;
        targetPosition = Vector3.zero;
        distance = 0f;
        status = DroneStatusEnum.Grounded;
        lastUpdateTime = Time.time;
    }
    
    /// <summary>
    /// Parametreli constructor
    /// </summary>
    public SmartDroneData(int droneId, Vector3 pos, Vector3 vel, Vector3 target)
    {
        id = droneId;
        position = pos;
        velocity = vel;
        targetPosition = target;
        distance = Vector3.Distance(pos, target);
        batteryLevel = 100f;
        signalStrength = 100f;
        status = DroneStatusEnum.Hovering;
        lastUpdateTime = Time.time;
    }
    
    /// <summary>
    /// Drone verisini güncelle
    /// </summary>
    public void UpdateData(Vector3 newPos, Vector3 newVel, Vector3 newTarget, DroneStatusEnum newStatus)
    {
        position = newPos;
        velocity = newVel;
        targetPosition = newTarget;
        distance = Vector3.Distance(newPos, newTarget);
        status = newStatus;
        lastUpdateTime = Time.time;
    }
    
    /// <summary>
    /// Drone'un hareket halinde olup olmadığını kontrol et
    /// </summary>
    public bool IsMoving()
    {
        return velocity.magnitude > 0.1f;
    }
    
    /// <summary>
    /// Drone'un hedefe ne kadar yakın olduğunu kontrol et
    /// </summary>
    public bool IsNearTarget(float threshold = 1f)
    {
        return distance < threshold;
    }
    
    /// <summary>
    /// Batarya düşük mü kontrol et
    /// </summary>
    public bool IsLowBattery()
    {
        return batteryLevel < 20f;
    }
    
    /// <summary>
    /// Sinyal zayıf mı kontrol et
    /// </summary>
    public bool IsWeakSignal()
    {
        return signalStrength < 30f;
    }
    
    /// <summary>
    /// Data string representation
    /// </summary>
    public override string ToString()
    {
        return $"Drone {id}: Pos={position}, Vel={velocity.magnitude:F1}m/s, Dist={distance:F1}m, Status={status}";
    }
}

/// <summary>
/// Drone Durum Enum'u - Finite State Machine için
/// </summary>
public enum DroneStatusEnum
{
    Grounded,           // Yerde
    Armed,              // Silahlandırılmış (Hazır)
    TakingOff,          // Kalkış yapıyor
    Hovering,           // Sabit uçuş
    Staging,            // Hazırlık pozisyonuna gidiyor
    FormationMove,      // Formasyon pozisyonuna gidiyor
    FormationHold,      // Formasyon pozisyonunu koruyor
    NavigationMove,     // Navigasyon hareketi
    Landing,            // İniş yapıyor
    Emergency,          // Acil durum
    Maintenance         // Bakım modu
}

/// <summary>
/// Formasyon Türleri
/// </summary>
public enum FormationType
{
    None,               // Formasyon yok
    VFormation,         // V Formasyonu
    ArrowFormation,     // Ok Başı Formasyonu
    LineFormation,      // Çizgi Formasyonu
    VerticalFormation,  // Dikey Sütun
    CircleFormation,    // Dairesel Formasyon
    CustomFormation     // Özel Formasyon
}

/// <summary>
/// Navigasyon Waypoint Verisi
/// </summary>
[System.Serializable]
public class WaypointData
{
    public int waypointId;              // Waypoint kimliği
    public Vector3 position;            // 3D pozisyon
    public float reachTime;             // Ulaşma süresi (T1)
    public float waitTime;              // Bekleme süresi (T2)
    public bool isReached;              // Ulaşıldı mı?
    public float reachTimestamp;        // Ulaşma zamanı
    
    public WaypointData(int id, Vector3 pos, float reach, float wait)
    {
        waypointId = id;
        position = pos;
        reachTime = reach;
        waitTime = wait;
        isReached = false;
        reachTimestamp = 0f;
    }
}

/// <summary>
/// Sürü İstatistikleri
/// </summary>
[System.Serializable]
public class SwarmStatistics
{
    [Header("Swarm Metrics")]
    public int totalDrones;             // Toplam drone sayısı
    public int activeDrones;            // Aktif drone sayısı
    public int dronesInFormation;       // Formasyondaki drone sayısı
    
    [Header("Performance")]
    public float averageSpeed;          // Ortalama hız
    public float formationAccuracy;     // Formasyon hassasiyeti (%)
    public float communicationHealth;   // İletişim sağlığı (%)
    
    [Header("Mission Progress")]
    public int completedWaypoints;      // Tamamlanan waypoint sayısı
    public float missionProgress;       // Görev ilerleme yüzdesi
    public float totalMissionTime;      // Toplam görev süresi
    
    public SwarmStatistics()
    {
        totalDrones = 0;
        activeDrones = 0;
        dronesInFormation = 0;
        averageSpeed = 0f;
        formationAccuracy = 100f;
        communicationHealth = 100f;
        completedWaypoints = 0;
        missionProgress = 0f;
        totalMissionTime = 0f;
    }
}

/// <summary>
/// TEKNOFEST Parametreleri
/// </summary>
[System.Serializable]
public class TeknoFestParameters
{
    [Header("TEKNOFEST Competition Parameters")]
    [Tooltip("Z - Uçuş İrtifası (metre)")]
    public float ucusIrtifasi = 10f;
    
    [Tooltip("T - Formasyon Koruma Süresi (saniye)")]
    public float formasyonKorumaSuresi = 30f;
    
    [Tooltip("X - Ajanlar Arası Mesafe (metre)")]
    public float ajanlarArasiMesafe = 5f;
    
    [Header("Navigation Parameters")]
    [Tooltip("T1 - Ara Nokta Ulaşma Süresi (saniye)")]
    public float araNokta_UlasmaSuresi_T1 = 10f;
    
    [Tooltip("T2 - Ara Noktada Bekleme Süresi (saniye)")]
    public float araNokta_BeklemeSuresi_T2 = 15f;
    
    [Header("System Parameters")]
    public int maxDroneCount = 50;      // Maksimum drone sayısı
    public float safetyMargin = 0.8f;   // Güvenlik marjı
    
    /// <summary>
    /// Parametreleri doğrula
    /// </summary>
    public bool ValidateParameters()
    {
        bool isValid = true;
        
        if (ucusIrtifasi <= 0 || ucusIrtifasi > 100)
        {
            Debug.LogError("❌ Geçersiz uçuş irtifası: " + ucusIrtifasi);
            isValid = false;
        }
        
        if (formasyonKorumaSuresi <= 0 || formasyonKorumaSuresi > 300)
        {
            Debug.LogError("❌ Geçersiz formasyon koruma süresi: " + formasyonKorumaSuresi);
            isValid = false;
        }
        
        if (ajanlarArasiMesafe <= 0 || ajanlarArasiMesafe > 20)
        {
            Debug.LogError("❌ Geçersiz ajanlar arası mesafe: " + ajanlarArasiMesafe);
            isValid = false;
        }
        
        return isValid;
    }
    
    /// <summary>
    /// Güvenli parametreler uygula
    /// </summary>
    public void ApplySafeParameters()
    {
        ucusIrtifasi = Mathf.Clamp(ucusIrtifasi, 5f, 50f);
        formasyonKorumaSuresi = Mathf.Clamp(formasyonKorumaSuresi, 10f, 120f);
        ajanlarArasiMesafe = Mathf.Clamp(ajanlarArasiMesafe, 2f, 15f);
        araNokta_UlasmaSuresi_T1 = Mathf.Clamp(araNokta_UlasmaSuresi_T1, 5f, 60f);
        araNokta_BeklemeSuresi_T2 = Mathf.Clamp(araNokta_BeklemeSuresi_T2, 5f, 60f);
    }
}

/// <summary>
/// Çarpışma Önleme Verisi
/// </summary>
[System.Serializable]
public class CollisionData
{
    public int droneId1;                // İlk drone ID
    public int droneId2;                // İkinci drone ID
    public float distance;              // Aralarındaki mesafe
    public Vector3 avoidanceVector;     // Kaçınma vektörü
    public float riskLevel;             // Risk seviyesi (0-1)
    public float timestamp;             // Tespit zamanı
    
    public CollisionData(int id1, int id2, float dist, Vector3 avoid, float risk)
    {
        droneId1 = id1;
        droneId2 = id2;
        distance = dist;
        avoidanceVector = avoid;
        riskLevel = risk;
        timestamp = Time.time;
    }
}

/// <summary>
/// İletişim Verisi
/// </summary>
[System.Serializable]
public class CommunicationData
{
    public int senderId;                // Gönderen drone ID
    public int receiverId;              // Alıcı drone ID (-1 = broadcast)
    public string messageType;          // Mesaj türü (SYN, ACK, DATA, etc.)
    public float signalStrength;        // Sinyal gücü
    public float latency;               // Gecikme (ms)
    public bool isSuccessful;           // Başarılı mı?
    public float timestamp;             // Gönderim zamanı
    
    public CommunicationData()
    {
        senderId = -1;
        receiverId = -1;
        messageType = "";
        signalStrength = 100f;
        latency = 0f;
        isSuccessful = true;
        timestamp = Time.time;
    }
}

/// <summary>
/// Yardımcı Matematik Fonksiyonları
/// </summary>
public static class SwarmMathUtils
{
    /// <summary>
    /// Vector3 listesinin ağırlık merkezini hesapla
    /// </summary>
    public static Vector3 CalculateCenterOfMass(List<Vector3> positions)
    {
        if (positions.Count == 0) return Vector3.zero;
        
        Vector3 center = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            center += pos;
        }
        
        return center / positions.Count;
    }
    
    /// <summary>
    /// İki pozisyon arasındaki 2D mesafeyi hesapla (Y ekseni hariç)
    /// </summary>
    public static float Distance2D(Vector3 pos1, Vector3 pos2)
    {
        Vector2 p1 = new Vector2(pos1.x, pos1.z);
        Vector2 p2 = new Vector2(pos2.x, pos2.z);
        return Vector2.Distance(p1, p2);
    }
    
    /// <summary>
    /// Açıyı -180 ile +180 derece arasında normalize et
    /// </summary>
    public static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    
    /// <summary>
    /// Dairesel interpolation
    /// </summary>
    public static Vector3 CircularLerp(Vector3 center, float radius, float startAngle, float endAngle, float t)
    {
        float angle = Mathf.LerpAngle(startAngle, endAngle, t) * Mathf.Deg2Rad;
        return center + new Vector3(
            Mathf.Cos(angle) * radius,
            0f,
            Mathf.Sin(angle) * radius
        );
    }
}

/// <summary>
/// Sistem Events
/// </summary>
public static class SwarmEvents
{
    public static System.Action<int> OnDroneSpawned;
    public static System.Action<int> OnDroneDestroyed;
    public static System.Action<FormationType> OnFormationChanged;
    public static System.Action<int, Vector3> OnWaypointReached;
    public static System.Action<bool> OnCommunicationStatusChanged;
    public static System.Action<CollisionData> OnCollisionRiskDetected;
    public static System.Action<SwarmStatistics> OnSwarmStatisticsUpdated;
}