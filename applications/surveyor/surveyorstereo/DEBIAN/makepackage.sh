cd ..
rm -r temp
mkdir temp
mkdir temp/DEBIAN
mkdir temp/usr
mkdir temp/usr/bin
mkdir temp/usr/bin/sentience
mkdir temp/usr/bin/sentience/surveyorstereo
cp DEBIAN/control temp/DEBIAN
cp DEBIAN/install temp/DEBIAN
cp DEBIAN/copyright temp/DEBIAN
cp DEBIAN/postinst temp/DEBIAN
cp DEBIAN/surveyorstereo.sh temp/usr/bin
ln -s /usr/bin/surveyorstereo.sh temp/usr/bin/surveyorstereo
cp bin/Debug/*.* temp/usr/bin/sentience/surveyorstereo
dpkg -b temp DEBIAN/surveyorstereo.deb
cd DEBIAN
alien -r surveyorstereo.deb
