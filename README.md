# CoordinateConverter
Convert coordinates to and from L/L, L/L Decimal, UTM and MGRS as well as Bullseye offsets.

# How to use

### Basic use
1. Select the coordinate system you want to convert from using the tabs in the input panel.
2. Enter the coordinates you wish to convert. More details on valid inputs down below.  
Invalid inputs will mark the relevant textbox pink to indicate the error.
3. When the input is valid, the conversion will be written to the output in various formats.
4. An altitude in `ft` or `m` may be entered and a label may be provided.

- Clicking the Add button will add the point to the list on the right.
- Clicking the `-` button in the list will delete that point from the list. The delete key may also be used to delete selected rows.
- Click the "Del" header above those buttons to delete all points in the list. A warning will be provided to avoid accidental deletion.
- Changing the radio boxes next to the output fields, changes the format in the list.
- Double clicking an entry in the list will put the coordinates into the input boxes for adjustments
- Clicking the Edit button will mark any selected entry for editing. If multiple entries are selected, only the altitude, point type, and label can be altered and it will alter all of them.
- Clicking the up and down buttons will move the selected entries in the list up or down.

### Use with DCS World
When properly installed (see installation instructions [here](https://github.com/FalcoGer/CoordinateConverter/blob/master/CoordinateConverter/Installation.txt) or in the release zip archive), then the program will attempt to automatically connect to the socket opened by the lua script. That socket is a TCP socket and listens on localhost on port 42020. Another listening socket on the same port must not be opened. [DCS TheWay](https://github.com/aronCiucu/DCSTheWay) uses a different port, both applications can be used simultaneously.

The connection will only occur during the exports setup, that is when a game world is loaded and unpaused.  
Upon connection the DCS status will turn green and the camera coordinates will be displayed.

- Clicking those coordinates, or the fetch coordinates button or the fetch coordinates menu entry puts them into the input fields to be edited and also puts into the list in edit mode.
- Selecting Auto from the DCS menu in the aircraft submenu will load the settings for that aircraft automatically.
- Deselecting Auto will allow the manual selection of an aircraft. This can be useful if you want to add aircraft specific point data to points and save that extra data to load later or share with someone else.
- More than one aircraft's specific point data can be stored. If an aircraft doesn't have specific point data, the relevant fields will be disabled in the application.
- Clicking the transfer button or selecting the transfer menu item from the DCS menu will input the coordinates from the data grid (the list on the right) into the aircraft in DCS. The correct aircraft and station (for multicrew) must be selected. Some other constraints must also be true that you will be reminded off when the speciic aircraft is being selected.
- Only points that have a checkmark in the XFER column will be transferred. Click the header to turn all checkmarks on or off at the same time. Select multiple by holding control and/or shift while clicking and pressing spacebar also turns all the selected rows on or off.

# L/L - Latitude/Longitude with decimal seconds

## Usage for entering L/L
Coordinates may be entered with their units, such as  
`45° 23' 13.1234"`  
Spaces are optional and can be used to visualize the breaks between degrees, minutes and seconds.  
`45 23 13.1234`  
Leading zeros must be included and zeroed out minutes and seconds must be included as well. The decimal part is optional.  
`02 00 00` would be 2° N/S.  
For Longitude, 3 digits are required for the degrees.

## Explaination of L/L
L/L assumes a spherical earth. The earth is divided up into degrees, minutes and seconds.  
1° = 60', or 60 minutes  
and 1' = 60", or 60 seconds.  
Additional fractions of seconds may be added behind a decimal point.  
Each point in space has a unique coordinate, except for the poles at +/- 90° latitude, where any longitude displays the same point.

### Latitude
The North/South component is called Latitude. It divides the earth into 180 slices.
Slices north of the equator are positive, labeled N. Slices south of the equator are negative values, labeled S.
### Longitude

The East/West component is called Longitude and divides the earth into 360° slices of Longitude along the equator.
180 of those slices are west of the meridian, indicated with a negative value, or the letter W.
180 of them are east, indicated with a positive value or E.

# L/L - Decimal minutes

## Usage for entering L/L with decimal minutes
Coordinates may be entered with their units, such as  
`45° 23.1234'`  
Spaces are optional and can be used to visualize the breaks between degrees, minutes and seconds.  
`45 23.1234'`  
Invalid inputs will mark the textbox pink to indicate the error.
Leading zeroes must be included and zeroed out minutes and seconds must be included as well. The decimal part is optional.  
`02 00` would be 2° N/S.  
For Longitude, 3 digits are required for the degrees.

## Explaination of L/L with decimal minutes
L/L with decimal minutes uses the same coordinate system as L/L.  
Except for seconds and fractions of seconds, those are converted into fractions of whole minutes.

# MGRS

## Usage for entering MGRS
Enter the UTM Grid number indicating the Longitude band into the first textbox. Valid numbers are 01 through 60. Leading zeros are required.
Enter the Latitude band Index into the second textbox. Valid input is any letter, except O and I. A and B are reserved for the south pole and Y and Z are reserved for the north pole. They use a special, polar coordinate system that is not supported by this application.  
Enter the sub grid ident, called digraph, into the third textbox.  
Enter your easting and your northing into the fourth textbox. Decimals are not allowed. Only even numbers of digits are allowed, including none, in which case 00 is assumed.  
`37 | T | GG | 46245002`

## Explaination of MGRS
MGRS, or Military Grid Reference System, uses the same grid layout as UTM.  
Each of those grids is divided into sub grids, indexed with a two letters digraph. Valid ranges are A-Z, excluding I and O, for the first letter and A-V, excluding I and O, for the second letter. Those grids are 10000m x 10000m size.  
From those sub grids you indicate a specific position with a set of numbers. Depending on the length of those numbers, you can indicate precision in up to 1m steps.  

Numbers&nbsp;&nbsp; | Precision \[m\]
--------------------|----------------
0                   | 100 000
2                   |  10 000
4                   |   1 000
6                   |     100
8                   |      10
10                  |       1

# UTM

## Usage for entering UTM
Enter the UTM Grid number indicating the Longitude band into the first textbox. Valid numbers are 01 through 60. Leading zeros are required.
Enter the Latitude band Index into the second textbox. Valid input is any letter, except O and I.  
Enter your easting and your northing in m into textbox 3 and 4. Decimals are allowed. Unit names are not allowed and meters are implied.  
`37 | T | 743300 | 4624500`

## Explaination of UTM
UTM, or Universal Transverse Mercator, divides the earth into a grid of 20 bands of latitude labeled C through X, omitting I and O, 60 bands of longitude labeled 01 through 60. From that grid's south west corner, you go to the east and to the north, indicated by the easting and northing part of the coordinates in meters.  
Some UTM grids have been adjusted to include countries so they are not split.  
The poles are separated into Grids A/B for the south pole and Y/Z for the north pole. Those grids are not supported.

# Bullseye

## Usage for entering Bullseye coordinates
A valid bullseye must be set by entering it's coordinates into the input and then pressing the set bullseye button. This will set that input as the bullseye.  
Enter a bearing from 0 through 360 in whole degrees. Decimals are allowed.  
Enter a range in nautical miles. Decimals are allowed.

## Explaination of Bullseye
The bullseye is an arbitrary point chosen by a coalition in a military operation to be used as a reference point across all participants of that operation.  
From that point, any other point can be identified by an offset from this point in degrees, called a bearing, and a range, typically in nautical miles.
