using UnityEngine;
using System.Collections;

// Emits a single (or small random burst of) particle(s) at random intervals -
// for a sparse "pre-rain" droplet effect before the full storm kicks in.
//
// Setup: on the Particle System's Emission module, set Rate over Time and
// Rate over Distance to 0, and remove/clear any Bursts - this script does
// the emitting manually via ParticleSystem.Emit(), so the system itself
// shouldn't be spawning anything on its own.
public class PreRainEmitter : MonoBehaviour
{
    [Tooltip("The rain droplet Particle System to emit from.")]
    public ParticleSystem drops;

    [Header("Interval Between Drops (seconds)")]
    public float minInterval = 0.4f;
    public float maxInterval = 2.5f;

    [Header("Particles Per Drop")]
    public int minCount = 1;
    public int maxCount = 1;

    [Tooltip("Starts emitting automatically on Start(). Leave off to control it manually via StartEmitting()/StopEmitting().")]
    public bool autoStart = true;

    private Coroutine emitRoutine;

    private void Start()
    {
        if (autoStart) StartEmitting();
    }

    public void StartEmitting()
    {
        if (emitRoutine != null) return;
        emitRoutine = StartCoroutine(EmitLoop());
    }

    public void StopEmitting()
    {
        if (emitRoutine == null) return;
        StopCoroutine(emitRoutine);
        emitRoutine = null;
    }

    private IEnumerator EmitLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (drops != null)
            {
                drops.Emit(Random.Range(minCount, maxCount + 1));
            }
        }
    }
}
