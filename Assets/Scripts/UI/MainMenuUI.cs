using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject HUDRoot;
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
        playerController.UI.Enable();
    }

    public void Hide()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        playerController.UI.Disable();
    }

    public void OnClickContinue()
    {
        Hide();

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadInitialRoom("YellowForest_TreeBranch", "Spawn");
            HUDRoot.SetActive(true);
            SFXManager.Instance.StopSFX(SFXType.WindBlowing);
    }
    public void OnClickQuit()
    {
        Application.Quit();
        print("Quit");
    }
}
