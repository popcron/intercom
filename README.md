# Intercom
Used for making two applications communicate with each other by using shared memory buffers. An example use case of this is avoiding Unity destroying everything when a domain reload happens (compiling while playing the game), by off loading the important code into a seperate process and then relaying it back to the main application.

## Requirements
- .NET Framework 4.5
- Git

## Installation
To install for local use, download this repo and copy everything from this repository to `<YourUnityProject>/Packages/Popcron Intercom` folder.

If using 2018.3.x or higher, you can add a new entry to the manifest.json file in your Packages folder:
```json
"com.popcron.intercom": "https://github.com/popcron/intercom.git"
```

## How to
Both application A and B need to use the same unique identifier (so that they could find each other). So when creating a new instance of an intercom, ensure that the identifier passed is the same for both sides. However, the first parameter, which is the Foo or Bar setting, one application must be Foo, and the other must be Bar (doesnt matter which one).
```cs
Intercom intercom = new Intercom(IntercomSide.Foo, "Game1"); //in Game1.exe
```
```cs
Intercom intercom = new Intercom(IntercomSide.Bar, "Game2"); //in Game2.exe
```

**Basically:**
1. When application A starts, make new intercom with Foo
2. When application B starts, make new intercom there with Bar
3. Tada!

## Sending methods
To send a method, call `Intercom.Invoke(methodName, parameters)` method.

Invoking a method isnt going to be instant, so if you need to ensure that the code after the invoke line is actually ran after the method is ran on the other side, then you can use the `InvokeTask` variant of the method instead to wait for that period.
```cs
Intercom intercom = new Intercom(IntercomSide.Foo, "Game1");
await intercom.InvokeTask("Game.DeleteSave");

//this will run after the Game.DeleteSave method ran in the other application
CreateCustomSaveFile();
```
*Note: Invoking methods that are meant to be awaitable on the other side, are not going to be awaited for.*

## Receiving methods
Decorating a method with the `[Invokey]` attribute will let the intercom system know that this **static** method can be automatically called upon, assuming the name and parameters match. Yes, duplicates of these are allowed.

If you'd like to raw dog this, you can alternatively pass a method as a delegate to the `Poll` method, which will then call upon that method for you to process with manually.

```cs
Intercom intercom = new Intercom(IntercomSide.Foo, "Game1");
while (true)
{
    intercom.Poll(OnInvoked);
    await Task.Delay(1);
}

//will get called when the other application sends something our way
private void OnInvoke(Invocation inv)
{
    if (inv.MethodName == "Boo") //do whatcha want
    {
    
    }
}
```
## Custom serializer
In the case of Unity, you might not want to use the default binary formatter in favour of the built in `JsonUtility` class, or whatever you'd like really. To do this, inherit from the `Serializer` class, implement the abstract methods, and provide your intercom objects with a new instance of your custom serializer.
```cs
Intercom intercom = new Intercom(IntercomSide.Foo, "Game1");
intercom.Serializer = new JsonSerializer(); // <--- your custom thingy
```

## License
MIT, do whatever you want.
