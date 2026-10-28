# Combat damage display

The native client draws floating damage values with its original digit texture.
Open the testing helper with **F12**, choose **Diagnostics**, and use **Combat
damage numbers** to adjust them while the game is running.

- **Size:** 75–200% of the original damage-number size. The default is 125%.
- **Weight:** Normal, Bold, or Heavy. Bold is the default. This thickens the
  digit edges using the existing texture; it does not change the game font.

Choose **Save damage style** to apply the change immediately and keep it in the
client's `config.ini`. This is a local visual preference; it does not change
damage calculations, server settings, or other UI text. The helper's display
controls work even when the testing server plugin is unavailable.
