using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;
using Mirror;

public class SpeechRecognitionEngine : NetworkBehaviour
{
    public string[] keywords = new string[] { "up", "down", "left", "right", "Fireball" };
    public ConfidenceLevel confidence = ConfidenceLevel.Medium;

    private PhraseRecognizer recognizer;
    private string word;
    private static bool keywordRecognizerStarted = false;

    [Header("Spell Cast")]
    public PlayerMagicSystem playerMagicSystem;


    private void Start()
    {
        if (keywordRecognizerStarted) return;
        if (!isLocalPlayer) return;

        if (keywords != null && keywords.Length > 0)
        {
            recognizer = new KeywordRecognizer(keywords, confidence);
            recognizer.OnPhraseRecognized += Recognizer_OnPhraseRecognized;
            recognizer.Start();
            Debug.Log("Recognizer running: " + recognizer.IsRunning);
        }

        foreach (var device in Microphone.devices)
        {
            Debug.Log("Microphone: " + device);
        }
        keywordRecognizerStarted = true;




    }

    private void Recognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        word = args.text;
        Debug.Log("You said: " + word);

        if (playerMagicSystem != null)
        {
            playerMagicSystem.CastSpellByVoice(word);
        }
    }

    private void OnApplicationQuit()
    {
        if (recognizer != null && recognizer.IsRunning)
        {
            recognizer.OnPhraseRecognized -= Recognizer_OnPhraseRecognized;
            recognizer.Stop();
        }
    }
}

