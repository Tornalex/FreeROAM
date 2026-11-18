using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject HUDRoot;
    [SerializeField] private GameObject player;
    private PlayerInputActions playerController;

    void Awake()
    {
        playerController = new PlayerInputActions();
        Show();
        SFXManager.Instance.PlaySFX(SFXType.WindBlowing); 
    }
    public void Show()
    {
        if (overlayRoot != null) overlayRoot.SetActive(true);
        player.SetActive(false);
        playerController.UI.Enable();
    }

    public void Hide()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        playerController.UI.Disable();
        player.SetActive(true);
    }

    public void OnClickContinue()
    {
        Hide();

        if (GameSceneManager.Instance != null)
            SFXManager.Instance.StopSFX(SFXType.WindBlowing);
            GameSceneManager.Instance.LoadInitialRoom("YellowForest_TreeBranch", "Spawn");
            HUDRoot.SetActive(true);
    }
    public void OnClickQuit()
    {
        Application.Quit();
        print("Quit");
    }
}
