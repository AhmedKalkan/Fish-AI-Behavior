using UnityEngine;
using System.Collections;
using System.Reflection;

public class BalikAksiyon : MonoBehaviour
{
    public enum MucadeleTipi { Standart, Tembel_OluGibi, DibeBasan_Gucu, KafaAtan_Asabi }

    [Header("🐟 Balık Karakteristiği")]
    public MucadeleTipi karakter = MucadeleTipi.Standart;

    [HideInInspector]
    public float balikGucuCarpani = 1f;

    [Header("💀 Ragdoll & Ölüm Süresi")]
    public float ragdollTavanY_Farki = 0.05f;
    public float maxHavadaKalmaSuresi = 30f;

    [Header("Çekiş Hızları")]
    public float yorgunCekilmeHizi = 1.5f; // Varsayılan değer toparlandı
    public float yuzeyCekilmeHizi = 3.0f;

    [Header("🌊 Yüzey Dinamiği (Göl İçin Sığ)")]
    public float yuzeyPesEtmeDerinligi = 0.15f;
    public float yuzeyDinlenmeSuresi = 3f;

    [Header("Mücadele Ayarları")]
    public float kacisHizi = 2.0f; // Stabilize edildi
    public float yonDegistirmeSuresi = 2.5f;

    [Header("Aksiyon Animasyonları (HookedSwim)")]
    public float hookedSwimAraligiMin = 2f;
    public float hookedSwimAraligiMax = 3f;
    public float hookedSwimSuresi = 2f;

    [Header("Oltada Asılı Kalma (Fizik Gücü)")]
    public float asilmaGucu = 15f;
    public float asilmaDonusHizi = 5f;
    public float gecisHizi = 10f;
    public float gecisSuresi = 0.5f;

    private Animator animator;
    private Rigidbody rootRb;
    private FishingLine misina;
    private Vector3 misinaBaglantiNoktasi;

    private bool suDisinda = false;
    private float hookedSwimSayaci = 0f;
    private bool hookedSwimOynuyor = false;

    private Coroutine patlamaCoroutine;
    private Coroutine gecisCoroutine;
    private bool gecisAsamasinda = false;
    private float yuzeyDinlenmeSayaci = 0f;

    private float sapmaZamani = 0f;
    private Vector3 anlikSapmaYonu = Vector3.forward;
    private Vector3 hedefYuzmeYonu = Vector3.forward;

    private Rigidbody[] kemikRigidbodleri;
    private Collider[] kemikColliderlari;

    private Transform anaKemikTransform;
    private Rigidbody anaKemikRb;
    private Transform agizNoktasi;

    private bool isOltada = true;
    private bool oluMu = false;
    private bool serbestBirakildi = false;
    private bool eveDonusBasladi = false;

    private float zemineMesafeTutucu = 5f;
    private float guncelRoll = 0f;
    private float secilenPatlamaRoll = 0f;
    private float anlikEsnemeMiktari = 0f;

    private float havadaKalmaSayaci = 0f;
    private bool canVerdi = false;

    private BalikSuruYonetici suruYonetici;
    private float suSeviyesiY = 9.83f;

    void Start()
    {
        animator = GetComponent<Animator>();
        rootRb = GetComponent<Rigidbody>();
        kemikRigidbodleri = GetComponentsInChildren<Rigidbody>();
        kemikColliderlari = GetComponentsInChildren<Collider>();

        misina = Object.FindFirstObjectByType<FishingLine>();
        if (misina == null) misina = GetComponentInParent<FishingLine>();

        BalikMotoru motor = GetComponent<BalikMotoru>();
        if (misina != null) suSeviyesiY = misina.suSeviyesiY;

        if (motor != null)
        {
            suruYonetici = motor.suruYonetici;
            if (suruYonetici != null) suSeviyesiY = suruYonetici.suSeviyesiY;

            motor.StopAllCoroutines();
            motor.enabled = false;
        }

        if (rootRb != null) { rootRb.isKinematic = true; rootRb.useGravity = false; }

        if (misina != null && misina.rodTip != null) misinaBaglantiNoktasi = misina.rodTip.position;
        else misinaBaglantiNoktasi = transform.position;

        anaKemikTransform = Bul(transform, "Bone");
        if (anaKemikTransform != null) anaKemikRb = anaKemikTransform.GetComponent<Rigidbody>();

        agizNoktasi = Bul(transform, "AgizNoktasi");
        if (agizNoktasi != null && anaKemikTransform != null) agizNoktasi.SetParent(anaKemikTransform, true);

        DipMekanigi dMekanik = GetComponent<DipMekanigi>();
        if (dMekanik != null) { dMekanik.StopAllCoroutines(); dMekanik.enabled = false; }

        SuruZekasi sZeka = GetComponent<SuruZekasi>();
        if (sZeka != null) { sZeka.enabled = false; }

        if (misina != null)
        {
            misina.kancadaBalikVar = true;
            misina.gerginHedef = agizNoktasi != null ? agizNoktasi : (anaKemikTransform != null ? anaKemikTransform : transform);
            misina.yemiGoster = false;
        }

        RagdollAktif(false);

        if (karakter == MucadeleTipi.KafaAtan_Asabi)
            hookedSwimSayaci = 0f;
        else
            hookedSwimSayaci = Random.Range(hookedSwimAraligiMin, hookedSwimAraligiMax);

        if (animator != null) animator.Play("Armature|Swim 1", 0, 0f);
    }

    Transform Bul(Transform parent, string isim)
    {
        if (parent.name == isim) return parent;
        foreach (Transform child in parent)
        {
            Transform bulundu = Bul(child, isim);
            if (bulundu != null) return bulundu;
        }
        return null;
    }

    void Update()
    {
        float guncelY_Kafa = anaKemikTransform != null ? anaKemikTransform.position.y : transform.position.y;

        if (!canVerdi)
        {
            if (guncelY_Kafa >= suSeviyesiY - 0.05f)
            {
                havadaKalmaSayaci += Time.deltaTime;
                if (havadaKalmaSayaci >= maxHavadaKalmaSuresi)
                {
                    canVerdi = true;
                    oluMu = true;
                    hookedSwimOynuyor = false;
                    if (animator != null) animator.enabled = false;
                    RagdollAktif(true);
                }
            }
            else
            {
                havadaKalmaSayaci = 0f;
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && isOltada)
        {
            isOltada = false;
            serbestBirakildi = true;

            Vector3 anlikİvme = anaKemikRb != null ? anaKemikRb.linearVelocity : Vector3.zero;
            Vector3 anlikDonus = anaKemikRb != null ? anaKemikRb.angularVelocity : Vector3.zero;

            SualtiKamerasi kameraSistemi = Object.FindFirstObjectByType<SualtiKamerasi>();
            if (kameraSistemi != null) kameraSistemi.BalikKactiVeyaAlindi();

            if (misina != null) { misina.kancadaBalikVar = false; misina.gerginHedef = null; }
            if (rootRb != null) { rootRb.isKinematic = true; rootRb.useGravity = false; }

            OltaSway oltaSway = Object.FindFirstObjectByType<OltaSway>();
            if (oltaSway != null) oltaSway.anlikAgirlik = 0f;

            RagdollAktif(true);

            foreach (Rigidbody rb in kemikRigidbodleri)
            {
                if (rb != null && rb.gameObject != this.gameObject)
                {
                    rb.linearVelocity = anlikİvme;
                    rb.angularVelocity = anlikDonus;
                }
            }
            return;
        }

        if (animator == null && !oluMu) return;

        if (oluMu && isOltada)
        {
            hookedSwimOynuyor = false;
            kacisHizi = 0f;
            balikGucuCarpani = 0.05f;
        }

        if (serbestBirakildi)
        {
            if (guncelY_Kafa < suSeviyesiY)
            {
                suDisinda = false;
                if (!canVerdi && !eveDonusBasladi)
                {
                    eveDonusBasladi = true;
                    FabrikaAyarlarinaDon();
                }
            }
            return;
        }

        if (oluMu) return;

        // ===========================================================================
        // MÜCADELE VE KUVVET (ETKİ TEPKİ) FİZİKLERİ
        // ===========================================================================
        if (misina != null && misina.rodTip != null) misinaBaglantiNoktasi = misina.rodTip.position;

        float misinaBoyuGuncel = misina != null ? misina.misinaUzunlugu : 2f;
        Vector3 olcumNoktasi = agizNoktasi != null ? agizNoktasi.position : transform.position;
        float mesafe = Vector3.Distance(olcumNoktasi, misinaBaglantiNoktasi);

        bool isGergin = mesafe >= misinaBoyuGuncel * 0.95f;

        // Sudan çıkarma sınırı silindi, arka planda güvenli 0.5 metre sınırı koyuldu
        bool makaraSariliyor = Input.GetMouseButton(0) && misinaBoyuGuncel > 0.5f;
        bool oltaAsiliyor = mesafe > misinaBoyuGuncel + 0.02f;
        bool baligaKuvvetUygulaniyor = isGergin && (makaraSariliyor || oltaAsiliyor);

        if (isOltada)
        {
            OltaSway gercekZamanliSway = Object.FindFirstObjectByType<OltaSway>();
            if (gercekZamanliSway != null)
            {
                float hedefAgirlik = 0f;

                if (isGergin)
                {
                    float temelAgirlik = Mathf.Abs(transform.localScale.x) * 8f;
                    if (guncelY_Kafa > suSeviyesiY) temelAgirlik *= 2.0f;

                    hedefAgirlik = temelAgirlik;
                    if (makaraSariliyor) hedefAgirlik += (temelAgirlik * 1.5f);
                    if (oltaAsiliyor)
                    {
                        float cekmeSiddeti = Mathf.Clamp01((mesafe - misinaBoyuGuncel) / 0.5f);
                        hedefAgirlik += (temelAgirlik * 2.0f * cekmeSiddeti);
                    }
                    if (hookedSwimOynuyor) hedefAgirlik += (temelAgirlik * 1.5f);
                    if (hedefAgirlik == 0f) hedefAgirlik = temelAgirlik * 0.1f;
                }

                float lerpHizi = (hedefAgirlik > gercekZamanliSway.anlikAgirlik) ? 15f : 5f;
                gercekZamanliSway.anlikAgirlik = Mathf.Lerp(gercekZamanliSway.anlikAgirlik, hedefAgirlik, lerpHizi * Time.deltaTime);
            }
        }

        bool balikYuzeydeMi = guncelY_Kafa >= suSeviyesiY - yuzeyPesEtmeDerinligi;
        bool ragdollTetiklendi = guncelY_Kafa >= (suSeviyesiY - ragdollTavanY_Farki);

        if (ragdollTetiklendi)
        {
            if (!suDisinda)
            {
                suDisinda = true;
                if (isOltada)
                {
                    if (gecisCoroutine != null) StopCoroutine(gecisCoroutine);
                    gecisCoroutine = StartCoroutine(KoparmaGecisi());
                }
            }

            if (isOltada && misina != null && makaraSariliyor)
            {
                misina.misinaUzunlugu -= (yuzeyCekilmeHizi / balikGucuCarpani) * Time.deltaTime;
                if (misina.misinaUzunlugu < 0.5f) misina.misinaUzunlugu = 0.5f;
            }
            if (isOltada) return;
        }
        else if (suDisinda)
        {
            bool gercektenSudaMi = guncelY_Kafa < (suSeviyesiY - ragdollTavanY_Farki - 0.05f);

            if (gercektenSudaMi)
            {
                suDisinda = false;
                if (gecisCoroutine != null) StopCoroutine(gecisCoroutine);
                gecisAsamasinda = false;

                if (anaKemikRb != null)
                {
                    transform.position = anaKemikRb.position;
                    Vector3 kemikIleri = anaKemikRb.transform.forward;
                    kemikIleri.y = 0f;
                    if (kemikIleri.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(kemikIleri.normalized);
                        hedefYuzmeYonu = kemikIleri.normalized;
                    }
                    guncelRoll = 0f;
                }

                if (!isOltada) isOltada = true;
                RagdollAktif(false);

                if (rootRb != null)
                {
                    rootRb.linearVelocity = Vector3.zero;
                    rootRb.angularVelocity = Vector3.zero;
                }

                if (animator != null) { animator.enabled = true; animator.Rebind(); animator.Play("Armature|Swim 1", 0, 0f); }
                if (rootRb != null) { rootRb.isKinematic = true; rootRb.useGravity = false; }
                if (misina != null) { misina.kancadaBalikVar = true; misina.gerginHedef = agizNoktasi != null ? agizNoktasi : (anaKemikTransform != null ? anaKemikTransform : transform); misina.yemiGoster = false; }
            }
            else if (isOltada && misina != null && makaraSariliyor)
            {
                misina.misinaUzunlugu -= (yuzeyCekilmeHizi / balikGucuCarpani) * Time.deltaTime;
                if (misina.misinaUzunlugu < 0.5f) misina.misinaUzunlugu = 0.5f;
                return;
            }
        }

        int zeminMask = ~(LayerMask.GetMask("Player", "Water", "Ignore Raycast"));
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit zeminHit, 5f, zeminMask)) zemineMesafeTutucu = zeminHit.distance;
        else zemineMesafeTutucu = 5f;

        bool yuzeydePesEtti = balikYuzeydeMi && !suDisinda;

        // 🎯 360 DERECE FIRILDAK BUG'I İÇİN MÜDAHALE (Gimbal Lock Önleyici)
        // Balık oltanın tam altına gelip kafayı dimdik gökyüzüne dikmesin diye Y eksenini yumuşattık
        Vector3 cekilenYonYatay = (misinaBaglantiNoktasi - transform.position);
        cekilenYonYatay.y *= 0.15f;
        if (cekilenYonYatay.sqrMagnitude < 0.01f) cekilenYonYatay = transform.forward;
        cekilenYonYatay.Normalize();

        if (Time.time > sapmaZamani)
        {
            Vector3 ileriYon = -cekilenYonYatay;
            Vector3 sagYon = Vector3.Cross(Vector3.up, cekilenYonYatay).normalized;

            switch (karakter)
            {
                case MucadeleTipi.DibeBasan_Gucu: anlikSapmaYonu = (ileriYon * 0.8f + sagYon * Random.Range(-0.8f, 0.8f) + Vector3.down * 2.5f).normalized; break;
                case MucadeleTipi.KafaAtan_Asabi: anlikSapmaYonu = (sagYon * Random.Range(-2.5f, 2.5f) + ileriYon * 0.5f).normalized; break;
                default: anlikSapmaYonu = (ileriYon * Random.Range(0.3f, 1.0f) + sagYon * Random.Range(-1.5f, 1.5f)).normalized; break;
            }
            sapmaZamani = Time.time + yonDegistirmeSuresi;
        }

        if (hookedSwimOynuyor)
        {
            if (yuzeydePesEtti) hedefYuzmeYonu = (anlikSapmaYonu + Vector3.down * 1.5f).normalized;
            else hedefYuzmeYonu = anlikSapmaYonu;
        }
        else
        {
            if (baligaKuvvetUygulaniyor) hedefYuzmeYonu = cekilenYonYatay;
            else hedefYuzmeYonu = anlikSapmaYonu;

            if (yuzeydePesEtti)
            {
                hedefYuzmeYonu.y -= 0.3f;
                hedefYuzmeYonu.Normalize();

                yuzeyDinlenmeSayaci += Time.deltaTime;
                float beklemeSuresi = (karakter == MucadeleTipi.KafaAtan_Asabi) ? Random.Range(0.5f, 1f) : yuzeyDinlenmeSuresi;
                if (yuzeyDinlenmeSayaci >= beklemeSuresi)
                {
                    yuzeyDinlenmeSayaci = 0f;
                    if (patlamaCoroutine != null) StopCoroutine(patlamaCoroutine);
                    patlamaCoroutine = StartCoroutine(HookedSwimOynatOzel(karakter == MucadeleTipi.KafaAtan_Asabi ? Random.Range(0.5f, 1f) : hookedSwimSuresi));
                }
            }
        }

        if (!yuzeydePesEtti && !hookedSwimOynuyor && !suDisinda)
        {
            hookedSwimSayaci -= Time.deltaTime;
            if (hookedSwimSayaci <= 0f)
            {
                if (patlamaCoroutine != null) StopCoroutine(patlamaCoroutine);
                patlamaCoroutine = StartCoroutine(HookedSwimOynat());
                hookedSwimSayaci = Random.Range(hookedSwimAraligiMin, hookedSwimAraligiMax);
            }
        }

        if (zemineMesafeTutucu <= 0.15f && hedefYuzmeYonu.y < 0) hedefYuzmeYonu.y = 0f;
        if (hookedSwimOynuyor && transform.position.y > suSeviyesiY - 0.1f && hedefYuzmeYonu.y > 0) hedefYuzmeYonu.y = 0f;

        hedefYuzmeYonu.Normalize();

        if (!baligaKuvvetUygulaniyor && !suDisinda)
        {
            if (Physics.Raycast(transform.position, hedefYuzmeYonu, out RaycastHit kiyiHit, 1.5f, zeminMask))
            {
                Vector3 kiyiNormali = kiyiHit.normal;
                kiyiNormali.y = 0f;
                Vector3 yansima = Vector3.Reflect(hedefYuzmeYonu, kiyiNormali).normalized;
                hedefYuzmeYonu = Vector3.Lerp(hedefYuzmeYonu, yansima, 8f * Time.deltaTime).normalized;
            }
        }

        YuzuHareketeCevir(hedefYuzmeYonu, baligaKuvvetUygulaniyor, yuzeydePesEtti, cekilenYonYatay, hookedSwimOynuyor);

        float aktifKacisHizi = kacisHizi;
        float mevcutHiz = aktifKacisHizi;

        if (baligaKuvvetUygulaniyor)
        {
            mevcutHiz = 0f;
        }
        else if (hookedSwimOynuyor)
        {
            mevcutHiz = aktifKacisHizi * 1.5f;
        }
        else if (!isGergin)
        {
            mevcutHiz = aktifKacisHizi * 0.6f;
        }
        else
        {
            mevcutHiz = aktifKacisHizi * 0.8f;
        }

        Vector3 hareketYonu = transform.forward;

        float aktifLimitY_Titreme = isGergin ? (suSeviyesiY - ragdollTavanY_Farki) : suSeviyesiY;
        float boneOfsetY = (anaKemikTransform != null) ? (anaKemikTransform.position.y - transform.position.y) : 0f;
        float tavanLimiti = aktifLimitY_Titreme - boneOfsetY;

        if (!suDisinda && transform.position.y >= tavanLimiti - 0.05f && hareketYonu.y > 0)
        {
            hareketYonu.y = 0f;
            hareketYonu.Normalize();
        }

        Vector3 sonrakiPoz = transform.position + (hareketYonu * mevcutHiz) * Time.deltaTime;

        if (isOltada && misina != null)
        {
            float kucukSinir = (suruYonetici != null) ? suruYonetici.maxKucukBoyut : 0.13f;
            float balikBoyutu = Mathf.Abs(transform.localScale.x);
            bool kucukBalikMi = balikBoyutu <= kucukSinir;

            float m_oyuncuCekmeGucu = misina.oyuncuCekmeGucu;
            float m_misinaEsnekligi = misina.misinaEsnekligi;
            float m_maxEsneme = 0.5f / Mathf.Max(m_misinaEsnekligi, 1f);

            float kalamaAlimi = 0f;

            if (!yuzeydePesEtti || (yuzeydePesEtti && karakter == MucadeleTipi.KafaAtan_Asabi && hookedSwimOynuyor))
            {
                if (!kucukBalikMi)
                {
                    float balikAnlikGucu = aktifKacisHizi * balikGucuCarpani * balikBoyutu;
                    float asilGuc = hookedSwimOynuyor ? (balikAnlikGucu * 2.0f) : (balikAnlikGucu * 0.5f);

                    if (makaraSariliyor)
                        kalamaAlimi = Mathf.Max(0f, asilGuc - m_oyuncuCekmeGucu);
                    else
                        kalamaAlimi = asilGuc * 0.8f;
                }
            }

            misina.misinaUzunlugu += kalamaAlimi * Time.deltaTime;
            if (misina.misinaUzunlugu > misina.maxMisinaUzunlugu) misina.misinaUzunlugu = misina.maxMisinaUzunlugu;

            if (baligaKuvvetUygulaniyor && DuzHatKontrolu())
            {
                float cekimHizi = (yuzeydePesEtti ? yuzeyCekilmeHizi : yorgunCekilmeHizi) / balikGucuCarpani;
                if (oltaAsiliyor) cekimHizi += (mesafe - misinaBoyuGuncel) * 2.5f;
                sonrakiPoz = Vector3.MoveTowards(sonrakiPoz, misinaBaglantiNoktasi, cekimHizi * Time.deltaTime);
            }

            Vector3 yeniMerkezdenBaliga = sonrakiPoz - misinaBaglantiNoktasi;
            if (yeniMerkezdenBaliga.magnitude > misina.misinaUzunlugu)
            {
                Vector3 idealYon = yeniMerkezdenBaliga.normalized;
                Vector3 mevcutYon = (transform.position - misinaBaglantiNoktasi).normalized;
                if (mevcutYon == Vector3.zero) mevcutYon = idealYon;

                Vector3 direncUygulanmisYon = Vector3.Slerp(mevcutYon, idealYon, (m_misinaEsnekligi * 0.2f) * Time.deltaTime);

                float hedefEsneme = 0f;
                if (baligaKuvvetUygulaniyor || hookedSwimOynuyor)
                    hedefEsneme = m_maxEsneme;

                anlikEsnemeMiktari = Mathf.Lerp(anlikEsnemeMiktari, hedefEsneme, 5f * Time.deltaTime);
                float aktifUzunluk = misina.misinaUzunlugu + anlikEsnemeMiktari;

                Vector3 gerginPozisyon = misinaBaglantiNoktasi + (direncUygulanmisYon * aktifUzunluk);

                if (!suDisinda && gerginPozisyon.y > tavanLimiti)
                {
                    gerginPozisyon.y = tavanLimiti;
                }

                sonrakiPoz = Vector3.Lerp(sonrakiPoz, gerginPozisyon, m_misinaEsnekligi * Time.deltaTime);
            }
            else
            {
                anlikEsnemeMiktari = Mathf.Lerp(anlikEsnemeMiktari, 0f, 5f * Time.deltaTime);
            }
        }

        if (!suDisinda && sonrakiPoz.y > tavanLimiti)
        {
            sonrakiPoz.y = tavanLimiti;
        }

        if (Physics.Raycast(sonrakiPoz + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 2f, zeminMask))
        {
            if (sonrakiPoz.y < hit.point.y + 0.1f) sonrakiPoz.y = hit.point.y + 0.1f;
        }

        transform.position = sonrakiPoz;
    }

    void FixedUpdate()
    {
        if (oluMu || serbestBirakildi) return;

        if (isOltada && suDisinda && misina != null && misina.rodTip != null && anaKemikRb != null)
        {
            Vector3 sarsilmazHedef = misina.rodTip.position + (Vector3.down * misina.misinaUzunlugu);
            Vector3 fizikselCekimMerkezi = agizNoktasi != null ? agizNoktasi.position : anaKemikRb.position;

            Vector3 dir = sarsilmazHedef - fizikselCekimMerkezi;

            float aktifMaxHiz = gecisAsamasinda ? gecisHizi : 50f;
            float aktifLerp = gecisAsamasinda ? 8f : 15f;

            float dinamikGuc = Mathf.Clamp(dir.magnitude * asilmaGucu, 0f, aktifMaxHiz);
            Vector3 hedefHiz = dir.normalized * dinamikGuc;

            anaKemikRb.linearVelocity = Vector3.Lerp(anaKemikRb.linearVelocity, hedefHiz, aktifLerp * Time.fixedDeltaTime);

            Vector3 yukariBak = (misina.rodTip.position - fizikselCekimMerkezi).normalized;
            if (yukariBak != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(yukariBak);
                Quaternion delta = targetRot * Quaternion.Inverse(anaKemikRb.rotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;

                Vector3 hedefAcisalHiz = axis * angle * Mathf.Deg2Rad * asilmaDonusHizi;
                hedefAcisalHiz = Vector3.ClampMagnitude(hedefAcisalHiz, 4f);

                anaKemikRb.angularVelocity = Vector3.Lerp(anaKemikRb.angularVelocity, hedefAcisalHiz, 5f * Time.fixedDeltaTime);
            }
        }
    }

    void RagdollAktif(bool aktifMi)
    {
        if (animator != null) animator.enabled = !aktifMi;

        if (kemikRigidbodleri.Length == 1 && kemikRigidbodleri[0] == rootRb)
        {
            rootRb.isKinematic = !aktifMi;
            rootRb.useGravity = aktifMi;
        }
        else
        {
            foreach (Rigidbody kemikRb in kemikRigidbodleri)
            {
                if (kemikRb == rootRb) continue;
                kemikRb.isKinematic = !aktifMi;
                kemikRb.useGravity = aktifMi;
                if (aktifMi && !oluMu)
                {
                    kemikRb.linearVelocity = Vector3.zero;
                    kemikRb.angularVelocity = Vector3.zero;
                }
            }
        }

        foreach (Collider col in kemikColliderlari)
        {
            if (kemikRigidbodleri.Length > 1 && col.gameObject == this.gameObject) continue;
            col.enabled = aktifMi;
        }
    }

    void YuzuHareketeCevir(Vector3 bakisYonu, bool zorlaCekiliyor, bool yuzeydePesEtti, Vector3 cekilenYon, bool patliyor)
    {
        if (bakisYonu.sqrMagnitude > 0.001f)
        {
            Quaternion hedefRot = Quaternion.LookRotation(bakisYonu, Vector3.up);
            Vector3 euler = hedefRot.eulerAngles;

            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;

            float idealRoll = 0f;

            if (yuzeydePesEtti)
            {
                float yerelX = transform.InverseTransformDirection(cekilenYon).x;
                float targetMaxRoll = zorlaCekiliyor ? 90f : 45f;

                idealRoll = Mathf.Clamp(yerelX * 150f, -targetMaxRoll, targetMaxRoll);
                pitch = Mathf.Clamp(pitch, -20f, 15f);
            }
            else
            {
                if (patliyor)
                {
                    idealRoll = secilenPatlamaRoll;
                }
                else if (zorlaCekiliyor)
                {
                    float yerelX = transform.InverseTransformDirection(cekilenYon).x;
                    idealRoll = Mathf.Clamp(yerelX * 100f, -35f, 35f);
                }
                pitch = Mathf.Clamp(pitch, -45f, 45f);
            }

            guncelRoll = Mathf.Lerp(guncelRoll, idealRoll, 6f * Time.deltaTime);
            hedefRot = Quaternion.Euler(pitch, euler.y, guncelRoll);

            // 🎯 YÜZEYDEKİ FIRILDAKLIĞA 2. DARBE: Yüzeyde ağır (1.5f), patlamada hızlı döner.
            float donusHizi = patliyor ? 3.5f : (yuzeydePesEtti ? 1.5f : 2.5f);
            transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, donusHizi * Time.deltaTime);
        }
    }

    bool DuzHatKontrolu()
    {
        if (misina == null || misina.rodTip == null) return false;
        Vector3 baslangic = misina.rodTip.position;
        Vector3 hedef = agizNoktasi != null ? agizNoktasi.position : transform.position;
        Vector3 yon = (hedef - baslangic).normalized;
        float mesafe = Vector3.Distance(baslangic, hedef);

        if (Physics.Raycast(baslangic, yon, out RaycastHit hit, mesafe))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return false;
        }
        return true;
    }

    void FabrikaAyarlarinaDon()
    {
        if (anaKemikRb != null)
        {
            transform.position = anaKemikRb.position;
            Vector3 kemikIleri = anaKemikRb.transform.forward;
            kemikIleri.y = 0f;
            if (kemikIleri.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(kemikIleri.normalized);
            guncelRoll = 0f;
        }

        RagdollAktif(false);

        if (rootRb != null)
        {
            rootRb.useGravity = false;
            rootRb.isKinematic = false;
            rootRb.interpolation = RigidbodyInterpolation.Interpolate;
            rootRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rootRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rootRb.linearVelocity = Vector3.zero;
            rootRb.angularVelocity = Vector3.zero;
            rootRb.WakeUp();
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Play("Armature|Swim 1", 0, 0f);
        }

        if (suruYonetici != null) transform.parent = suruYonetici.transform;

        BalikMotoru motor = GetComponent<BalikMotoru>();
        if (motor != null)
        {
            motor.StopAllCoroutines();
            motor.yemModu = false;
            GizliDegiskeniSifirla(motor, "turnOynuyor", false);
            GizliDegiskeniSifirla(motor, "sonTurnZamani", -999f);

            if (suruYonetici != null)
            {
                Vector3 suruyeDogru = suruYonetici.suruMerkezi - transform.position;
                suruyeDogru.y = 0f;
                if (suruyeDogru.sqrMagnitude > 0.1f) motor.hedefYon = suruyeDogru.normalized;
            }
            motor.enabled = true;
        }

        DipMekanigi dip = GetComponent<DipMekanigi>();
        if (dip != null)
        {
            dip.StopAllCoroutines();
            GizliDegiskeniSifirla(dip, "dogalBesleniyor", false);
            GizliDegiskeniSifirla(dip, "slideYapiyor", false);
            GizliDegiskeniSifirla(dip, "beslenmeAnimasyonuAktif", false);
            GizliDegiskeniSifirla(dip, "slideAnimasyonuAktif", false);
            GizliDegiskeniSifirla(dip, "siradakiBeslenmeZamani", Time.time + Random.Range(5f, 15f));
            GizliDegiskeniSifirla(dip, "siradakiSlideZamani", Time.time + Random.Range(15f, 30f));
            dip.enabled = true;
        }

        SuruZekasi zeka = GetComponent<SuruZekasi>();
        if (zeka != null) zeka.enabled = true;

        this.enabled = false;
    }

    private void GizliDegiskeniSifirla(object hedefScript, string degiskenAdi, object yeniDeger)
    {
        if (hedefScript == null) return;
        FieldInfo field = hedefScript.GetType().GetField(degiskenAdi, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) field.SetValue(hedefScript, yeniDeger);
    }

    IEnumerator HookedSwimOynat()
    {
        hookedSwimOynuyor = true;
        int rand = Random.Range(0, 3);
        if (rand == 0) secilenPatlamaRoll = -40f;
        else if (rand == 1) secilenPatlamaRoll = 40f;
        else secilenPatlamaRoll = 0f;

        if (animator != null && !suDisinda) animator.Play("Armature|Swim 2", 0, 0f);
        yield return new WaitForSeconds(hookedSwimSuresi);
        if (animator != null && !suDisinda) animator.Play("Armature|Swim 1", 0, 0f);
        hookedSwimOynuyor = false;
    }

    IEnumerator HookedSwimOynatOzel(float sure)
    {
        hookedSwimOynuyor = true;
        if (animator != null && !suDisinda) animator.Play("Armature|Swim 2", 0, 0f);
        yield return new WaitForSeconds(sure);
        if (animator != null && !suDisinda) animator.Play("Armature|Swim 1", 0, 0f);
        hookedSwimOynuyor = false;
    }

    IEnumerator KoparmaGecisi()
    {
        gecisAsamasinda = true;
        RagdollAktif(true);

        yield return new WaitForSeconds(gecisSuresi);

        gecisAsamasinda = false;

        foreach (Rigidbody kemikRb in kemikRigidbodleri)
        {
            if (kemikRb != null && kemikRb.gameObject != this.gameObject)
            {
                kemikRb.useGravity = true;
            }
        }
    }
}