#import <SafariServices/SafariServices.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
 
extern UIViewController * UnityGetGLViewController();
 
extern "C"
{
  @interface SafariViewController : UIViewController<SFSafariViewControllerDelegate>
  @end
 
  @implementation SafariViewController
  - (void)safariViewControllerDidFinish:(SFSafariViewController *)controller {
    NSLog(@"safariViewControllerDidFinish");
  }
  @end
 
  
  SafariViewController * svc;
  const char * loginUrl;

  void InternalLaunchSafariWebViewUrl(const char * url, bool allowCookie)
  {
    NSLog(@"Launching SFSafariViewController");

    // Get the instance of ViewController that Unity is displaying now
    UIViewController * uvc = UnityGetGLViewController();

    NSMutableString *urlMutableString = [NSMutableString stringWithString:[[NSString alloc] initWithUTF8String:url]];
    if (!allowCookie)
    {
      [urlMutableString appendString:@"&extra_hide_cookie=true&extra_hide_onetrust=true"];
    }
      
    // Generate an NSURL object based on the C string passed from C#
    NSURL * URL = [NSURL URLWithString: urlMutableString];

    // Create an SFSafariViewController object from the generated URL
    SFSafariViewController * sfvc = [[SFSafariViewController alloc] initWithURL:URL];

    // Assign a delegate to handle when the user presses the 'Done' button
    svc = [[SafariViewController alloc] init];
    sfvc.delegate = svc;

    // Start the generated SFSafariViewController object
    [uvc presentViewController:sfvc animated:YES completion:nil];

    NSLog(@"Presented SFSafariViewController");
  }
 
  void LaunchSafariWebViewUrl(const char * url)
  {
    NSLog(@"Launching SFSafariViewController");
    loginUrl = url;
    const char * found;
    const char * loginPart = "/authorize?";
    found = strstr(url, loginPart);
    if (!found)
    {
      NSLog(@"LaunchSafariWebViewUrl without cookie parameters.");
      InternalLaunchSafariWebViewUrl(loginUrl, true);
    }
    else
    {
      if (@available(iOS 14, *))
      {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status)
        {
          if (status == ATTrackingManagerAuthorizationStatusAuthorized)
          {
            NSLog(@"requestTrackingAuthorizationWithCompletionHandler AUTHORIZED");
            InternalLaunchSafariWebViewUrl(loginUrl, true);
          }
          else
          {
            NSLog(@"requestTrackingAuthorizationWithCompletionHandler DENIED");
            InternalLaunchSafariWebViewUrl(loginUrl, false);
          }
        }];
      }
      else
      {
        // Fallback
        InternalLaunchSafariWebViewUrl(loginUrl, true);
      }
    }
  }

  void DismissSafariWebView()
  {
    NSLog(@"DismissSafariWebView");
    UIViewController * uvc = UnityGetGLViewController();
    [uvc dismissViewControllerAnimated:YES completion:nil];
  }

  void RequestAppTrackingAuthorization() {
    if (@available(iOS 14, *)) {

      id handler = ^(NSUInteger result) {
        NSLog(@"Result request tracking authorization : %lu", (unsigned long)result);
      };

      SEL requestSelector = NSSelectorFromString(@"requestTrackingAuthorizationWithCompletionHandler:");
      if ([ATTrackingManager respondsToSelector:requestSelector]) {
          [ATTrackingManager performSelector:requestSelector withObject:handler];
      }
    }  
  }
  
}