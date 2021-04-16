#import <Foundation/Foundation.h>

char* convertNSStringToCString(const NSString* nsString)
{
    if (nsString == NULL)
    {
      return NULL;
    }

    const char* nsStringUtf8 = [nsString UTF8String];
    //create a null terminated C string on the heap so that our string's memory isn't wiped out right after method's return
    char* cString = (char*)malloc(strlen(nsStringUtf8) + 1);
    strcpy(cString, nsStringUtf8);

    return cString;
}

extern "C"
{
  char* GetUserLocale()
  {
    NSLocale *currentLocale = [NSLocale currentLocale];
    NSString *countryCode = [currentLocale objectForKey: NSLocaleCountryCode];
    return convertNSStringToCString(countryCode);
  }
}