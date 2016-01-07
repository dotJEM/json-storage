$path = "$($env:appveyor_build_folder)"
$instance = ".\DEV"

# create new empty json database for tests.
$mdf = join-path $path "json.mdf"
$ldf = join-path $path "json.ldf"
sqlcmd -S "$instance" -Q "Use [master]; CREATE DATABASE [json] ON ( NAME = json_dat, FILENAME = '$mdf' ) LOG ON ( NAME = json_log, FILENAME = '$ldf' )"