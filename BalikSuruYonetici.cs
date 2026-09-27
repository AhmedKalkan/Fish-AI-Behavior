using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BalikSuruYonetici : MonoBehaviour
{
    [Header("Sürü Ayarları")]
    public GameObject balikPrefab;
    public int balikSayisi = 5;
    public Vector3 suruMerkezi;

    [Header("🐟 KÜÇÜK Balık Ayarları")]
    [Tooltip("Doğma ihtimali (Yüzde)")]
    public float kucukIhtimali = 60f;
    public float minKucukBoyut = 0.05f;
    public float maxKucukBoyut = 0.08f;
    [Tooltip("BalikAksiyon koduna gidecek Güç Çarpanı")]
    public float kucukGucCarpani = 0.5f;

    [Header("🐟 ORTA Balık Ayarları")]
    [Tooltip("Doğma ihtimali (Yüzde)")]
    public float ortaIhtimali = 30f;
    public float minOrtaBoyut = 0.09f;
    public float maxOrtaBoyut = 0.15f;
    [Tooltip("BalikAksiyon koduna gidecek Güç Çarpanı")]
    public float ortaGucCarpani = 1.0f;

    [Header("🐟 BÜYÜK Balık Ayarları")]
    [Tooltip("Doğma ihtimali (Kalan yüzde otomatik büyük olur)")]
    public float minBuyukBoyut = 0.16f;
    public float maxBuyukBoyut = 0.3f;
    [Tooltip("BalikAksiyon koduna gidecek Güç Çarpanı")]
    public float buyukGucCarpani = 2.0f;

    [Header("Sürü Alanı (Gizmo Kutusu)")]
    public float alanGenislikX = 8f;
    public float alanDerinlikZ = 4f;
    public float alanYukseklikY = 0.5f;

    [Header("Hızlar")]
    public float minHiz = 0.5f;
    public float maxHiz = 1.5f;
    public float donusHizi = 2f;

    [Header("Sürü Kuralları")]
    public float ayrimaMesafesi = 1.5f;
    public float gorusMesafesi = 4f;
    public float ayrimaGucu = 1.5f;
    public float hizalanmaGucu = 1f;
    public float birlesmeGucu = 0.5f;
    public float merkezeDonusGucu = 2f;

    [Header("Su ve Kıyı Sınırları")]
    public float suSeviyesiY = 9.83f;
    public float kiyiSiniriZ = -6.9f;

    private List<Transform> baliklar = new List<Transform>();

    void Start()
    {
        BaliklariOlustur();
    }

    void BaliklariOlustur()
    {
        int duvarLayer = LayerMask.GetMask("Duvar");

        // LEGO SİSTEMİ: Üstünde DipMekanigi scripti varsa dipte doğar
        bool isDipBaligi = (balikPrefab.GetComponent("DipMekanigi") != null);

        // 🎯 1. ADIM: KUTUNUN İÇİNDE SÜRÜNÜN KOMPLE DOĞACAĞI "TEK BİR RASTGELE NOKTA" SEÇİYORUZ
        Vector3 rastgeleSuruDogumMerkezi = suruMerkezi + new Vector3(
            Random.Range(-alanGenislikX / 2f, alanGenislikX / 2f),
            Random.Range(-alanYukseklikY / 2f, alanYukseklikY / 2f),
            Random.Range(-alanDerinlikZ / 2f, alanDerinlikZ / 2f)
        );

        float dogumYaricapi = 1.5f; // Sürü balıklarının birbirine yakın (omuz omuza) doğma çapı

        for (int i = 0; i < balikSayisi; i++)
        {
            // 🎯 2. ADIM: Sürü elemanlarını o seçilen tek merkezin etrafında sıkı sıkı doğurtuyoruz
            Vector3 rastgelePoz = rastgeleSuruDogumMerkezi + new Vector3(
                Random.Range(-dogumYaricapi, dogumYaricapi),
                Random.Range(-0.2f, 0.2f), // Y ekseninde çok saçılmasınlar, hizalı kalsınlar
                Random.Range(-dogumYaricapi, dogumYaricapi)
            );

            if (isDipBaligi)
            {
                RaycastHit hit;
                Vector3 yukaridanBakis = new Vector3(rastgelePoz.x, suSeviyesiY, rastgelePoz.z);
                if (Physics.Raycast(yukaridanBakis, Vector3.down, out hit, Mathf.Infinity, duvarLayer))
                    rastgelePoz.y = hit.point.y + 0.05f;
            }
            else
            {
                float kutuAltSinir = suruMerkezi.y - (alanYukseklikY / 2f);
                float kutuUstSinir = suruMerkezi.y + (alanYukseklikY / 2f);
                rastgelePoz.y = Mathf.Clamp(rastgelePoz.y, kutuAltSinir, kutuUstSinir);
            }

            rastgelePoz.z = Mathf.Max(rastgelePoz.z, kiyiSiniriZ + 1f);

            GameObject yeniBalik = Instantiate(balikPrefab, rastgelePoz, Quaternion.identity);
            yeniBalik.transform.parent = this.transform;

            // 🎯 BOYUT VE GÜÇ HESAPLAMA MOTORU
            float zar = Random.Range(0f, 100f);
            float secilenBoyut = 0f;
            float secilenGuc = 1f;

            if (zar < kucukIhtimali)
            {
                secilenBoyut = Random.Range(minKucukBoyut, maxKucukBoyut);
                secilenGuc = kucukGucCarpani;
            }
            else if (zar < (kucukIhtimali + ortaIhtimali))
            {
                secilenBoyut = Random.Range(minOrtaBoyut, maxOrtaBoyut);
                secilenGuc = ortaGucCarpani;
            }
            else
            {
                secilenBoyut = Random.Range(minBuyukBoyut, maxBuyukBoyut);
                secilenGuc = buyukGucCarpani;
            }

            yeniBalik.transform.localScale = new Vector3(secilenBoyut, secilenBoyut, secilenBoyut);

            BalikAksiyon aksiyonKodu = yeniBalik.GetComponent<BalikAksiyon>();
            if (aksiyonKodu == null) aksiyonKodu = yeniBalik.GetComponentInChildren<BalikAksiyon>();

            if (aksiyonKodu != null)
            {
                aksiyonKodu.balikGucuCarpani = secilenGuc;
            }

            yeniBalik.SetActive(true);

            BalikMotoru motor = yeniBalik.GetComponent<BalikMotoru>();
            if (motor == null) motor = yeniBalik.AddComponent<BalikMotoru>();

            motor.suruYonetici = this;
            motor.balikIndex = i;

            baliklar.Add(yeniBalik.transform);

            // 🎯 ANİMASYON DESENKRONİZASYONU
            Animator anim = yeniBalik.GetComponent<Animator>();
            if (anim != null)
            {
                StartCoroutine(AnimasyonZamaniniRastgeleYap(anim));
            }
        }
    }

    IEnumerator AnimasyonZamaniniRastgeleYap(Animator anim)
    {
        yield return null;
        if (anim != null)
        {
            anim.Play("Armature|Swim 1", 0, Random.Range(0f, 1f));
        }
    }

    public List<Transform> YakindakiBaliklariGetir(Vector3 pozisyon, int kendiIndex)
    {
        List<Transform> yakindakiler = new List<Transform>();
        for (int i = 0; i < baliklar.Count; i++)
        {
            if (i != kendiIndex)
            {
                if (Vector3.Distance(pozisyon, baliklar[i].position) < gorusMesafesi)
                    yakindakiler.Add(baliklar[i]);
            }
        }
        return yakindakiler;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
        Gizmos.DrawCube(suruMerkezi, new Vector3(alanGenislikX, alanYukseklikY, alanDerinlikZ));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(suruMerkezi, new Vector3(alanGenislikX, alanYukseklikY, alanDerinlikZ));
    }
}