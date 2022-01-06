using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomDebug {

    public static string Debug(string TAG, string m) {
        return TAG + ": " + m;
    }

    public static string Debug(string TAG, string m1, string m2) {
        return TAG + ": " + m1 + "\n\t" + m2;
    }

}
