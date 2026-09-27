using UnityEngine;
using System.Collections.Generic;

public class SuruZekasi : MonoBehaviour
{
    public Vector3 SuruYonuHesapla(BalikMotoru motor)
    {
        if (motor.suruYonetici == null) return Vector3.zero;

        List<Transform> yakindakiler = motor.suruYonetici.YakindakiBaliklariGetir(transform.position, motor.balikIndex);
        if (yakindakiler.Count == 0) return Vector3.zero;

        Vector3 ayrilma = Vector3.zero;
        Vector3 hizalanma = Vector3.zero;
        Vector3 birlesme = Vector3.zero;
        int ayrilmaSayisi = 0;

        foreach (Transform diger in yakindakiler)
        {
            float mesafe = Vector3.Distance(transform.position, diger.position);
            if (mesafe < motor.suruYonetici.ayrimaMesafesi)
            {
                ayrilma += (transform.position - diger.position).normalized / mesafe;
                ayrilmaSayisi++;
            }

            BalikMotoru digerMotor = diger.GetComponent<BalikMotoru>();
            if (digerMotor != null) hizalanma += digerMotor.hedefYon;
            birlesme += diger.position;
        }

        if (ayrilmaSayisi > 0) ayrilma = ayrilma.normalized * motor.suruYonetici.ayrimaGucu;

        hizalanma = hizalanma.normalized * motor.suruYonetici.hizalanmaGucu;
        birlesme = ((birlesme / yakindakiler.Count) - transform.position).normalized * motor.suruYonetici.birlesmeGucu;

        return ayrilma + hizalanma + birlesme;
    }
}