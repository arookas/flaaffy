
## charge

The _charge_ errand allows you to extract and replace data stored within an AAF file.
An AAF file is known as an _initialization data file_, and is one of the formats used to initialize the audio subsystem in Super Mario Sunshine.
It contains many blocks of data grouped into numbered chunks to store initialization data for sequences, sounds, streams, waves banks, etc.

The first argument specifies the action to perform:

|Action|Description|
|------|-----------|
|-extract-seq|Extracts the data of a BMS sequence.|
|-replace-seq|Replaces the data for a BMS sequence.|
|-extract-ibnk|Extracts the data of a bank.|
|-extract-ibnk|Replaces the data for a bank.|
|-extract-wsys|Extracts the data of a wave bank.|
|-replace-wsys|Replaces the data of a wave bank.|

Following the action are the various arguments:

|Parameter|Description|
|---------|-----------|
|-init-data-file _&lt;file&gt;_ [_&lt;output&gt;_]|Specifies the path and filename to the AAF file. When replacing data inside an AAF, _&lt;output&gt;_ specifies the filename of the modified file; if omitted, ".new.aaf" will be appended to the source filename.|
|-seq-data-file _&lt;file&gt;_ [_&lt;output&gt;_]|Specifies the path and filename to the sequence archive file. When replacing sequences, _&lt;output&gt;_ specifies the filename of the modified file; if omitted, ".new.arc" will be appended to the source filename.|
|-input _&lt;file&gt;_|Specifies the source file containing the data with which to replace the old.|
|-output _&lt;file&gt;_|Specifies the destination file to which the extracted data will be dumped.|
|-target _&lt;target&gt;_|Specifies which sequence, bank, or wave bank to extract or replace. For sequences, you may specify the index, filename, or ASN name of the sequence. For banks and wave banks, only indices are supported.|

### Default sequences

In Super Mario Sunshine, there are a total of 48 sequences.
The first is special, as it contains not music but the sound-effect state machine.
For the AAF format, sequence filenames are truncated to 13 characters.
Alongside the filename, there is a list of more detailed names in the ASN data;
all sound categories, sound effects, streams, and sequences have names in this data.
The following list details the sequences found in the vanilla game:

|Index|Filename|ASN|Description|
|-----|--------|---|-----------|
|0|se.scom|MSD_SE_SEQ|Contains and manages sound effects.|
|1|k_dolpic.com|MSD_BGM_DOLPIC|Delfino Plaza|
|2|k_bianco.com|MSD_BGM_BIANCO|Bianco Hills|
|3|k_manma.com|MSD_BGM_MAMMA|Gelato Beach|
|4|t_pinnapaco_s|MSD_BGM_PINNAPACO_SEA|Pinna Park (beach)|
|5|t_pinnapaco.c|MSD_BGM_PINNAPACO|Pinna Park|
|6|t_mare_sea.co|MSD_BGM_MARE_SEA|Noki Bay (underwater)|
|7|t_montevillag|MSD_BGM_MONTEVILLAGE|Pianta Village|
|8|t_shilena.com|MSD_BGM_SHILENA|Sirena Beach|
|9|k_rico.com|MSD_BGM_RICCO|Ricco Harbor|
|10|k_clear.com|MSD_BGM_GET_SHINE|Shine!|
|11|t_chuboss.com|MSD_BGM_CHUBOSS|Polluted Piranha Plant boss battle|
|12|k_miss.com|MSD_BGM_MISS|Miss|
|13|t_boss.com|MSD_BGM_BOSS|Big boss battle (e.g. Petey Piranha)|
|14|t_select.com|MSD_BGM_MAP_SELECT|File select|
|15|t_bosspakkun_|MSD_BGM_BOSSPAKU_DEMO|Unknown|
|16|k_title.com|MSD_BGM_MAIN_TITLE|Title sequence|
|17|t_chuboss2.co|MSD_BGM_CHUBOSS2|Plungelo boss battle|
|18|k_ex.com|MSD_BGM_EXTRA|Secret course|
|19|t_delfino.com|MSD_BGM_DELFINO|Hotel Delfino|
|20|t_marevillage|MSD_BGM_MAREVILLAGE|Noki Bay|
|21|t_corona.com|MSD_BGM_CORONA|Corona Mountain|
|22|k_kagemario.c|MSD_BGM_KAGEMARIO|Shadow Mario|
|23|k_camera.com|MSD_BGM_CAMERA|Episode preview|
|24|t_montevillag|MSD_BGM_MONTE_ONSEN|Pianta Village (hot spring)|
|25|t_mechakuppa_|MSD_BGM_MECHAKUPPA|Mecha-Bowser battle|
|26|k_airport.com|MSD_BGM_AIRPORT|Delfino Airstrip|
|27|k_chika.com|MSD_BGM_UNDERGROUND|Manhole/underground|
|28|k_titleback.c|MSD_BGM_TITLEBACK|Title demo|
|29|t_montevillag|MSD_BGM_MONTE_NIGHT|Pianta Village (night)|
|30|t_delfino_kaj|MSD_BGM_CASINO|Hotel Delfino (casino)|
|31|t_event.com|MSD_BGM_EVENT|Event (e.g. rollercoaster)|
|32|t_timelimit.c|MSD_BGM_TIME_IVENT|Timed event|
|33|t_extra_skyan|MSD_BGM_SKY_AND_SEA|Sky & Sea|
|34|t_montevillag|MSD_BGM_MONTE_RESCUE|Piantas in Need|
|35|t_pinnapaco_m|MSD_BGM_MERRY_GO_ROUND|Pinna Park (merry-go-round)|
|36|k_select.com|MSD_BGM_SCENARIO_SELECT|Episode select|
|37|t_casino_fanf|MSD_BGM_FANFARE_CASINO|Casino fanfare|
|38|t_race_fanfar|MSD_BGM_FANFARE_RACE|Race fanfare|
|39|k_camerakage.|MSD_BGM_CAMERA_KAGE|Episode preview (Bianco 1)|
|40|k_gameover.co|MSD_BGM_GAMEOVER|Game over|
|41|t_boss_hanach|MSD_BGM_BOSSHANA_2ND3RD|Wiggler boss battle|
|42|t_boss_geso_i|MSD_BGM_BOSSGESO_2DN3RD|Glooper Blooper boss battle|
|43|t_chuboss_man|MSD_BGM_CHUBOSS_MANTA|Manta boss battle|
|44|t_montevillag|MSD_BGM_MONTE_LAST|Pianta Village (festival)|
|45|t_shine_appea|MSD_BGM_SHINE_APPEAR|Shine appears|
|46|k_kuppa.com|MSD_BGM_KUPPA|Bowser boss battle|
|47|t_monteman_ra|MSD_BGM_MONTEMAN_RACE|Il Piantissimo race|
