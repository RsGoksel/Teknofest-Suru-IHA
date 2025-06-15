# TEKNOFEST Sürü İHA Yarışması - Kritik Tasarım Raporu
## COMBINE Takımı - Hibrit Sürü Zekası Projesi

### 🚁 Proje Özeti
Bu proje, TEKNOFEST Sürü İHA Yarışması için geliştirilmiş hibrit sürü zekası sistemidir. Merkezi ve merkezi olmayan kontrol mimarilerinin dinamik geçişini sağlayan özgün bir yaklaşım benimsenmiştir.

### 📋 Takım Bilgileri
- **Takım Adı:** COMBINE
- **Başvuru ID:** 3620387
- **Takım ID:** 742853

### 👥 Takım Üyeleri
- **Göksel Gündüz** (Takım Kaptanı) - Sürü zekası algoritmaları, ArUco marker görüntü işleme
- **Aysima Şentürk** - Matematiksel modelleme, formasyon geçiş stratejileri
- **İrfan Gümüş** - Donanım entegrasyonu, elektronik sistemler
- **Gökhan Büyük** - Navigasyon, yol planlama algoritmaları
- **Zehra Kaya** - Simülasyon geliştirme, arayüz tasarımı

### 🎯 Özgün Katkılarımız
- **Hibrit Kontrol Mimarisi:** Merkezi ve merkezi olmayan kontrol sistemlerinin dinamik geçişi
- **TCP Handshake Protokolü:** Sürü üyeleri arası güvenilir haberleşme
- **A* Tabanlı Keşif:** ArUco marker tespiti için optimize edilmiş arama algoritması
- **Çarpışma Önleme:** Gelecek pozisyon tahmini ile proaktif kaçınma sistemi

### 🛠️ Teknoloji Stack
- **Platform:** Unity 3D (C#)
- **İletişim:** ROS2, PyMAVLink
- **Simülasyon:** Unity, Gazebo11 (SITL)
- **Donanım:** Raspberry Pi 4, GEPRC F405 Flight Controller
- **OS:** Ubuntu 24.04

### 📁 Proje Yapısı
```
├── Assets/
│   └── Scripts/
│       ├── Core/
│       │   ├── DroneSpawner.cs                 # Ana kontrol sistemi
│       │   ├── SmartDronePhysics.cs            # Drone fizik kontrolü
│       │   ├── DroneCommHub.cs                 # İletişim hub'ı
│       │   └── SmartDroneData.cs               # Veri yapıları
│       ├── Formation/
│       │   └── FormationGenerator.cs           # Formasyon hesaplayıcı
│       ├── Navigation/
│       │   └── NavigationController.cs         # Navigasyon sistemi (Navigasyon.txt'den)
│       └── Utils/
│           ├── CollisionAvoidanceSystem.cs     # Çarpışma önleme
│           └── CentralCommunicationHub.cs      # Merkezi hub

```

### 🎮 Desteklenen Formasyonlar
1. **V Formasyonu** - Dinamik kanat yapısı
2. **Ok Başı Formasyonu** - Uç-kuyruk-kanat konfigürasyonu
3. **Çizgi Formasyonu** - Yatay sıralama
4. **Dikey Sütun** - Dikey yığılma

### 📊 Sistem Özellikleri
- **Dinamik Drone Sayısı:** 1-50 İHA arası ölçeklenebilirlik
- **Runtime Parametre Güncelleme:** Jüri parametrelerinin canlı değişimi
- **Çarpışma Önleme:** Güvenlik yarıçapı tabanlı proaktif sistem
- **Fail-Safe Mekanizması:** İletişim kesilmesi durumunda otonom çalışma

### 🎬 Demonstrasyon Videoları
- [Çizgi Formasyonu (Dikey)](https://youtu.be/HYYeip-Mim8)
- [Çizgi Formasyonu ( Yatay)]
- [V Formasyonu](https://youtu.be/DuO_IYh8ixo)
- [Ok Formasyonu](https://youtu.be/IpQJxfUj--M)
- [Sürü Navigasyon](https://youtu.be/xiAAvgKC9-k)
- [Birey Ekleme-Çıkarma](https://youtu.be/rmu6ozI7tHk)

### 🔧 Kurulum ve Çalıştırma

#### Gereksinimler
- Unity 2022.3 LTS+
- Ubuntu 24.04
- ROS2 Humble
- Python 3.8+

#### Kurulum Adımları
```bash
# Repository'yi klonla
git clone https://github.com/RsGoksel/Teknofest-Suru-IHA.git
cd Teknofest-Suru-IHA

# Gerekli paketleri yükle
sudo apt update
sudo apt install ros-humble-desktop

# Python bağımlılıklarını yükle
pip install -r requirements.txt

# Unity projesini aç
# Unity Hub > Add > Simulations klasörünü seç
```

#### Çalıştırma
```bash
# ROS2 ortamını başlat
source /opt/ros/humble/setup.bash

# Simülasyonu başlat
cd Simulations/
# Unity'de DroneSpawner sahnesini aç ve Play'e bas
```

### 📐 Algoritma Özellikleri

#### Çarpışma Önleme Formülü
```
F_avoid = Σ(K × (R_safe - d_i) / R_safe × û_i)
```
- K: Kaçınma kuvveti sabiti (3.0)
- R_safe: Güvenlik yarıçapı (X × 0.8)
- d_i: İHA'ya olan mesafe
- û_i: Kaçınma yön vektörü

#### V Formasyonu Hesaplama
```
Sol Kanat: P_left(i) = (-X × i × 0.8, Z + 2.5 × i, 0)
Sağ Kanat: P_right(i) = (+X × i × 0.8, Z + 2.5 × i, 0)
Merkez: P_center = (0, Z, 0)
```

### 🏗️ Donanım Özellikleri
- **Frame:** 3K karbon fiber kompozit (350g)
- **Motor:** 820 KV BLDC brushless (4x)
- **ESC:** GEPRC F405 entegre 50A
- **İşlemci:** Raspberry Pi 4 (4GB RAM)
- **Kamera:** 480p Raspberry kamera (ArUco tespiti)
- **Batarya:** 3300 mAh 7.7V 2S 30C LiPo

### 📚 Teknik Dokümantasyon
- [Kritik Tasarım Raporu (KTR)](Documentation/KTR_Report.pdf)
- [Algoritma Detayları](Documentation/Algorithms.md)
- [Donanım Kılavuzu](Documentation/Hardware_Guide.md)
- [API Referansı](Documentation/API_Reference.md)

### 🔬 Test ve Doğrulama
- ✅ Formasyon geçişleri
- ✅ Çarpışma önleme
- ✅ Fail-safe mekanizmaları
- ✅ Dinamik parametre güncellemeleri
- ✅ Sürü navigasyonu

#### Gerekliler
```bash

Unity>=2022.3.0f1
ROS2-Humble
Ubuntu>=20.04
Python>=3.8
```


### 🙏 Teşekkürler
TEKNOFEST organizasyonu ve jüri üyelerine destekleri için teşekkür ederiz.

---
*Bu proje, hibrit sürü zekası alanında özgün katkılar sunmayı hedeflemektedir.*
