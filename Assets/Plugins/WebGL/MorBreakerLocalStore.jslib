// morBreaker — minimal bridge to the browser's localStorage for the high-score list.
// The game stores ONE key (a small JSON blob) entirely on the player's own device.
// Nothing is sent anywhere. Used only by Leaderboard.cs in WebGL builds.
mergeInto(LibraryManager.library, {
  // Returns a newly-malloc'd UTF8 string (caller frees via MorBreakerLSFree).
  MorBreakerLSGet: function (keyPtr) {
    var value = "";
    try {
      var key = UTF8ToString(keyPtr);
      value = window.localStorage.getItem(key) || "";
    } catch (e) {
      value = "";
    }
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  MorBreakerLSSet: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
    } catch (e) {
      // Quota exceeded or storage disabled (e.g. private mode): silently ignore.
    }
  },

  MorBreakerLSFree: function (ptr) {
    _free(ptr);
  }
});
