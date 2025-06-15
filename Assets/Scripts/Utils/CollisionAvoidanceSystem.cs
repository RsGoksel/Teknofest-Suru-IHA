// =================================================================
// ÇARPIŞMA ÖNLEME ALGORİTMASI - MATEMATİKSEL MODEL
// =================================================================
// AMAÇ: Sürü halindeki İHA'ların birbirleriyle çarpışmadan güvenli 
//       hareket etmesini sağlamak için öngörülü algoritma
// =================================================================

public class CollisionAvoidanceSystem
{
    // MATEMATİKSEL PARAMETRELER
    private float safetyRadius = 4f;        // Güvenlik yarıçapı (metre)
    private float predictionTime = 2f;      // Gelecek pozisyon tahmini (saniye)
    private float avoidanceStrength = 3f;   // Kaçınma kuvveti çarpanı
    
    /// <summary>
    /// ÇARPIŞMA ÖNLEME KUVVET HESAPLAMA
    /// Matematiksel Model: F_avoid = Σ(K * (R_safe - d_i) / R_safe * û_i)
    /// 
    /// Burada:
    /// F_avoid = Toplam kaçınma kuvveti vektörü
    /// K = Kaçınma kuvveti sabiti (avoidanceStrength)
    /// R_safe = Güvenlik yarıçapı (safetyRadius)
    /// d_i = i. drone'a olan mesafe
    /// û_i = i. drone'dan uzaklaşma yön vektörü (normalize)
    /// </summary>
    public Vector3 CalculateAvoidanceForce(Vector3 myPosition, List<DroneData> nearbyDrones)
    {
        Vector3 totalAvoidanceForce = Vector3.zero;
        
        foreach (DroneData otherDrone in nearbyDrones)
        {
            // 1. MESAFE HESAPLAMA
            float distance = Vector3.Distance(myPosition, otherDrone.position);
            
            // 2. GÜVENLİK YARIÇAPI KONTROLÜ
            if (distance < safetyRadius && distance > 0.1f)
            {
                // 3. GELECEK POZİSYON TAHMİNİ (Öngörülü Algoritma)
                // P_future = P_current + V_current * Δt
                Vector3 predictedPosition = otherDrone.position + otherDrone.velocity * predictionTime;
                
                // 4. KAÇINMA YÖN VEKTÖRÜ HESAPLAMA
                // û = (P_my - P_predicted) / |P_my - P_predicted|
                Vector3 avoidanceDirection = (myPosition - predictedPosition).normalized;
                
                // 5. KUVVET BÜYÜKLÜĞÜ HESAPLAMA (Ters Kare Yasası)
                // F_magnitude = K * (R_safe - d) / R_safe
                float forceMagnitude = avoidanceStrength * (safetyRadius - distance) / safetyRadius;
                
                // 6. TOPLAM KUVVET VEKTÖRÜ
                // F_i = F_magnitude * û_i
                totalAvoidanceForce += avoidanceDirection * forceMagnitude;
                
                // DEBUG: Kritik durumları logla
                if (distance < safetyRadius * 0.5f)
                {
                    Debug.LogWarning($"KRİTİK YAKINLIK: Mesafe={distance:F2}m, Kuvvet={forceMagnitude:F2}");
                }
            }
        }
        
        return totalAvoidanceForce;
    }
    
    /// <summary>
    /// GÜVENLİ YOL PLANLAMA ALGORİTMASI
    /// Amaç: Hedefe giden yolda çarpışma riski olan alanları tespit et
    ///       ve alternatif güvenli rotalar hesapla
    /// 
    /// Matematiksel Model: 
    /// P_safe = α * P_target + β * P_avoidance
    /// Burada α + β = 1 (normalize edilmiş ağırlıklar)
    /// </summary>
    public Vector3 CalculateSafePathVector(Vector3 currentPos, Vector3 targetPos, 
                                         List<DroneData> obstacles)
    {
        // 1. HEDEF YÖN VEKTÖRÜ
        Vector3 targetDirection = (targetPos - currentPos).normalized;
        
        // 2. ÇARPIŞMA ÖNLEME KUVVET HESAPLAMA
        Vector3 avoidanceForce = CalculateAvoidanceForce(currentPos, obstacles);
        
        // 3. AĞIRLIKLI KOMBİNASYON (Adaptive Weighting)
        // Tehlike seviyesine göre ağırlık ayarlama
        float dangerLevel = CalculateDangerLevel(currentPos, obstacles);
        float avoidanceWeight = Mathf.Clamp01(dangerLevel / safetyRadius);
        float targetWeight = 1f - avoidanceWeight;
        
        // 4. GÜVENLİ YOL VEKTÖRÜ HESAPLAMA
        Vector3 safePathVector = (targetDirection * targetWeight + 
                                avoidanceForce.normalized * avoidanceWeight).normalized;
        
        return safePathVector;
    }
    
    /// <summary>
    /// TEHLİKE SEVİYESİ HESAPLAMA
    /// Çevredeki drone yoğunluğuna göre tehlike seviyesi belirleme
    /// </summary>
    private float CalculateDangerLevel(Vector3 position, List<DroneData> obstacles)
    {
        float totalDanger = 0f;
        
        foreach (DroneData obstacle in obstacles)
        {
            float distance = Vector3.Distance(position, obstacle.position);
            if (distance < safetyRadius)
            {
                // Mesafe azaldıkça tehlike üstel olarak artar
                totalDanger += Mathf.Pow((safetyRadius - distance) / safetyRadius, 2);
            }
        }
        
        return totalDanger;
    }
}

// =================================================================
// KULLANIM ÖRNEĞİ - DRONE HAREKET KONTROLÜ
// =================================================================
public class SmartDroneMovement
{
    private CollisionAvoidanceSystem avoidanceSystem = new CollisionAvoidanceSystem();
    
    /// <summary>
    /// AKILLI HAREKET HESAPLAMA
    /// Hem hedefe gitme hem de çarpışma önleme kuvvetlerini birleştir
    /// </summary>
    public Vector3 CalculateSmartMovement(Vector3 currentPos, Vector3 targetPos, 
                                        List<DroneData> nearbyDrones, float moveForce)
    {
        // 1. GÜVENLİ YOL HESAPLAMA
        Vector3 safeDirection = avoidanceSystem.CalculateSafePathVector(
            currentPos, targetPos, nearbyDrones);
        
        // 2. MESAFE BAZLI KUVVET AYARLAMA
        // Hedefe yaklaştıkça kuvvet azalt (Yumuşak iniş için)
        float distance = Vector3.Distance(currentPos, targetPos);
        float forceMultiplier = Mathf.Clamp(distance / 2f, 0.8f, 2f);
        
        // 3. TOPLAM HAREKET KUVVETİ
        // F_total = F_direction * K_distance
        Vector3 totalForce = safeDirection * moveForce * forceMultiplier;
        
        return totalForce;
    }
}