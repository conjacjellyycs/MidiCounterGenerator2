How to make a Midi Counter Pattern?

1.Add a file in the folder, like 1.txt.
  The suffix of the filename must be .txt
  And the pattern ID will be the prefix before ".txt" like "1" in the example

2.Make pattern.
  Format below
--------------------------
{Video Width}
{Video Height}
{Font Name}
{Font Size}
{Pattern (multi lines)}
--------------------------
  The file 0.txt is an example.

Pattern variable list:
  {0}: Note Count
  {0,}: Note Count with a comma per 3 digits
  {1}: All notes
  {1,}: All notes with a comma per 3 digits
  {1-0}: Remaining notes
  {1-0,}: Remaining notes with a comma per 3 digits
  {2}: BPM
  {3}: Time (secs)
  {4}: NPS
  {4,}: NPS with a comma per 3 digits
  {5}: Polyphony
  {5,}: Polyphony with a comma per 3 digits
  {6}: Tick
  {6,}: Tick with a comma per 3 digits
  {7}: All tick
  {7,}: All tick with a comma per 3 digits
  {7-6}: Remaining ticks
  {7-6,}: Remaining ticks with a comma per 3 digits
  {8}: Beats
  {9}: All beats
  {9-8}: Remaining beats
  {A}: Resolution (PPQ)
  {A,}: Resolution (PPQ) with a comma per 3 digits
  {B}: Lyrics