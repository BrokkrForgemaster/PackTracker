namespace PackTracker.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void LoginButton_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Dashboard").ConfigureAwait(false);
    }
}
