/*
<!-- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -->
<!-- code copyright (c) 2016-2017 ProcGenesis                            -->
<!-- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -->
*/

importScripts('draw.min.js', 'perlin.js', 'seedrandom.js');

var elArray, moistArray, tempArray, windArray, wid, ht;
var sea_level;
var savedmrandstate;
var maxmoist = -Infinity;
var minmoist = Infinity;

self.addEventListener('message', function (e)
{
    var data = e.data;
    /*switch (data.cmd) {
    case 'start':
    self.postMessage('WORKER STARTED: ' + data.msg);
    break;
    case 'stop':
    self.postMessage('WORKER STOPPED: ' + data.msg +
    '. (buttons will no longer work)');
    self.close(); // Terminates the worker.
    break;
    default:
    self.postMessage('Unknown command: ' + data.msg);
    };*/


    //console.log("Data load function within worker");

    elArray = data.elevationArr;
    moistArray = data.moistureArr;
    tempArray = data.temperatureArr;
    windArray = data.windArr;
    wid = data.width;
    ht = data.height;
    sea_level = data.sl;
    savedmrandstate = data.savedmrand;

    distributeMoisture2_worker(moistArray, elArray, tempArray, windArray, wid, ht);
    //console.log("Inside Worker Load Function");
    //postMessage(attArray);

}, false);




function distributeMoisture2_worker(moisture_array, elevation_array, temperature_array, wind_array, width, height){

    //console.log("in-function");

    Math.seedrandom('', { state: savedmrandstate });

    var count = 0;

    var distributed_array = matrix(width, height, 0);
    var averaging_array = matrix(width, height, 0);
    //var pn = new Perlin(PerlinSeeds[5]);
    //var pn2 = new Perlin(PerlinSeeds[6]);

    var pn = new Perlin(Math.random());
    var pn2 = new Perlin(Math.random());

    var windspeed = 0;
    var windxcomp = 0;
    var windycomp = 0;
    var moisture_remaining = 0;
    var current_temp = 0;
    var last_elevation = 0;

    var unitxcomp = 0;
    var unitycomp = 0;

    //var xvec = x + Math.floor(windxcomp);
    //var yvec = y + Math.floor(windycomp);
    var xvec = 0;
    var yvec = 0;

    var unitxvec = 0;
    var unityvec = 0;

    var resultant_vecx = 0;
    var resultant_vecy = 0;

    var resultant_mag = 0;

    var el_slope = 0;

    var ny = 0;
    var nx = 0;
    var nz = 0;
    
    var value = 0;

    var stepCount = 0;

    //var lastvecx = 0;
    //var lastvecy = 0;
    var progresspercent = 0;

    //var t0 = performance.now();

    for(var y = 0; y < height; y++){
        for( var x = 0; x < width;x++){

    //var x = 799;
    //var y = 199;
            //console.log("in for loop " + x + ", " + y);
            //setTimeout(distributeMoistureLoop, 0);
            //distributeMoistureLoop();
            
                   
            //function distributeMoistureLoop(){
            ny = 4*y / height;
            
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            
            value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            value /= 1.28;
            value = Math.pow(value, 2);

            if (elevation_array[x][y] >= sea_level)
            {
                distributed_array[x][y] += .15 * value;
            }

            if (elevation_array[x][y] < sea_level) {
                //console.log("sea");
                windspeed = Math.sqrt(wind_array[x][y].xcomp * wind_array[x][y].xcomp + wind_array[x][y].ycomp * wind_array[x][y].ycomp);
                windxcomp = wind_array[x][y].xcomp;
                windycomp = wind_array[x][y].ycomp;
                moisture_remaining = moisture_array[x][y]*50;
                current_temp = temperature_array[x][y];
                last_elevation = elevation_array[x][y];

                unitxcomp = windxcomp / windspeed;
                unitycomp = windycomp / windspeed;

                //var xvec = x + Math.floor(windxcomp);
                //var yvec = y + Math.floor(windycomp);
                xvec = x + Math.round(unitxcomp);
                yvec = y + Math.round(unitycomp);

                unitxvec = 0;
                unityvec = 0;

                resultant_vecx = 0;
                resultant_vecy = 0;

                el_slope = 0;

                stepCount = 0;
                //console.log("pre while loop xvec: " + xvec + " yvec: " + yvec + " moisture_remaining: "+moisture_remaining + " moisture[x][y]: "+moisture_array[x][y] + "elevation: "+elevation_array[x][y]);

                while (moisture_remaining > .1 && stepCount < 1000) {
                    //console.log("in the while loop");
                    if(xvec >= width)
                    {
                        //console.log("Xwrap+");
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        //console.log("Xwrap-");
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {   
                        //console.log("broken");
                        break;
                        
                    }

                    //if (isNaN(xvec) || isNaN(yvec))
                    //{

                    //if (yvec == 0 || yvec == 1)
                    //{
                    //    console.log("xvec " + xvec + "yvec " + yvec);
                    //}
                    //}
                    // need to account for wrap on xvec and yvec
                    //distributed_array[xvec][yvec] += moisture_remaining * (1 / (windspeed + 7) + (.6 * elevation_array[xvec][yvec] / (15 * temperature_array[xvec][yvec] + .8)));

                    if (last_elevation >= sea_level){ //&& elevation_array[xvec][yvec] >= .35)
                        
                        
                        
                        //{
                        //el_slope = (elevation_array[xvec][yvec] - last_elevation);
                        //el_slope = (.599 * (elevation_array[xvec][yvec] - last_elevation)) - (.2 * temperature_array[xvec][yvec]) + (.2 * elevation_array[xvec][yvec]) - (.001 * windspeed);
                        
                        
                        el_slope = (elevation_array[xvec][yvec] - last_elevation)*(Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7);
                        //el_slope = elevation_array[xvec][yvec] - temperature_array[xvec][yvec] - .005 * windspeed + 1;
                        
                        
                        //console.log("ElSlope: " + el_slope + " Slope: "+ (elevation_array[xvec][yvec] - last_elevation) + " Temp: " + temperature_array[xvec][yvec] + " El: "+elevation_array[xvec][yvec] + " WS: "+windspeed);
                            //if (elevation_array[xvec][yvec] >= .35)
                            //{
                        //if (el_slope < .001)
                        //{
                        //    el_slope = .001;
                        //}
                       
                    }    
                    
                    
                    
                    else
                    {
                        //el_slope = .01*(elevation_array[xvec][yvec] - last_elevation);
                        
                        
                        
                        el_slope = .01*(elevation_array[xvec][yvec] - last_elevation)*(Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7);
                    }    
                    
                    
                    
                    //if (el_slope < .002){               //UNCOMMENT HERE
                    //    el_slope = .002;                //UNCOMMENT HERE
                    //}                                   //UNCOMMENT HERE

                    if(el_slope <= .002)
                    {
                        el_slope = .002;
                        //el_slope = Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7;
                        //console.log("negative elsope: "+el_slope);
                    }
                    
                        
                   /*if (elevation_array[xvec][yvec] < .35)
                   // {
                   //     var seaflag = true;
                        
                  //   }
                  //   else
                  //   {
                  //       var seaflag = false;

                  //   }   
                        
                        //el_slope *= 1.54; //convert range from 0-.65 to 0-1
                   // }
                   // else
                  // {
                   //     el_slope = 0;
                   // }

                   //if(elevation_array[xvec][yvec] < .35)
                   //{
                       //el_slope = 0;
                       //distributed_array[xvec][yvec] = 0;
                       //console.log("el < .35");
                  //}
                  // else
                  // {
                      // distributed_array[xvec][yvec] += moisture_remaining * el_slope;
                      // console.log("el > .35");
                  // }
                  */

                    distributed_array[xvec][yvec] += moisture_remaining * el_slope;
                    //averaging_array[xvec][yvec] += 1;
                    //distributed_array[xvec][yvec] += el_slope;
                    
                    
                    //distributed_array[xvec][yvec] += (.4 * moisture_remaining * el_slope * el_slope);     //TEST WITH THIS ONE !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //if (xvec < 100 && yvec < 100)
                    //{
                    //console.log("distributed arr: " + distributed_array[xvec][yvec] + " mremaining: " + moisture_remaining + " curr el: " + elevation_array[xvec][yvec] + " last el: " + last_elevation);
                    //}
                    
                    //if(elevation_array[xvec][yvec] < .35)
                    //{
                    //    distributed_array[xvec][yvec] = .001;
                    //}
                    
                      
                    //m * 1/(v + 7) + h/15t
                    //console.log("m_val " + distributed_array[xvec][yvec] + " m_remaining: "+moisture_remaining+" windspeed: "+windspeed+" elevation: "+elevation_array[xvec][yvec]+" temperature: "+temperature_array[xvec][yvec]);

                    //probably need to calculate the unit vector of the wind here - currently skipping a lot of pixels, only going to pixel where vector ends
                    windspeed = Math.sqrt(wind_array[xvec][yvec].xcomp * wind_array[xvec][yvec].xcomp + wind_array[xvec][yvec].ycomp * wind_array[xvec][yvec].ycomp);
                    //xvec += wind_array[xvec][yvec].xcomp;
                    //yvec += wind_array[xvec][yvec].ycomp;
                    //moisture_remaining -= distributed_array[xvec][yvec];
                    //moisture_remaining -= distributed_array[xvec][yvec];

                    

                     //console.log(seaflag);
                     
                    if (elevation_array[xvec][yvec] < sea_level){

                        //var tester = .0001;
                    //    console.log("in the statement");
                        //moisture_remaining -= distributed_array[xvec][yvec];
                       // distributed_array[xvec][yvec] = tester;
                    //    distributed_array[xvec][yvec] = 0;
                        distributed_array[xvec][yvec] = .0001;
                    }


                    if (elevation_array[xvec][yvec] > .6)
                    {

                        moisture_remaining -= 4*distributed_array[xvec][yvec];
                    }
                    else
                    {
                        moisture_remaining -= distributed_array[xvec][yvec];
                    }


                    last_elevation = elevation_array[xvec][yvec];

                    //console.log("unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);
                    //lastvecx = xvec;
                    //lastvecy = yvec;

                    unitxvec = Math.round(unitxcomp);
                    unityvec = Math.round(unitycomp);

                    xvec += unitxvec;
                    yvec += unityvec;

                    //console.log("testetstestsetsetsetet");

                    if(xvec >= width)
                    {
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {   
                        //console.log("broken2");
                        break;
                        //console.log("broken2");
                    }

                    //if(Number.isNaN(wind_array[xvec][yvec].xvec))
                    //{
                    //    console.log("WArrayx: "+wind_array[xvec][yvec].xvec+" xvec: "+xvec+" yvec: "+yvec);
                    //}

                    //console.log("testtesttest");

                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec);
                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec + " |||| unitxvec: "+ unitxvec + " unityvec: "+unityvec+" |||| unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);
                    //console.log("unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);

                    //if(isNaN(xvec))
                    //{
                        //console.log(wind_array);
                    //    console.log("last xvec " + lastvecx + " last yvec" + lastvecy);
                    //    console.log("resmag: " + resultant_mag + " resvecx: " + resultant_vecx + " resvecy: " + resultant_vecy);
                    //}
                    //console.log("wind_array[xvec][yvec].xcomp: " + wind_array[xvec][yvec].xcomp + " windxcomp: " + windxcomp);
                    resultant_vecx = wind_array[xvec][yvec].xcomp + windxcomp;
                    resultant_vecy = wind_array[xvec][yvec].ycomp + windycomp;
                    //console.log(resultant_vecx);
                    
                    resultant_mag = Math.sqrt(resultant_vecx * resultant_vecx + resultant_vecy * resultant_vecy);

                    if (resultant_mag != 0)
                    {
                        unitxcomp = resultant_vecx / resultant_mag;
                        unitycomp = resultant_vecy / resultant_mag;
                    }
                    else
                    {
                        //console.log("/0 caught");
                        //unitxcomp = 0;
                        //unitycomp = 0;
                        break; 
                    }


                    stepCount++;




                }
                  
            }



            //count++;
            //document.getElementById("ProgressPercent").innerHTML = count.toString();
            //}

            progresspercent += 1/(height*width);
            //drawLoadBar(6 / 9 + (progresspercent/12));

            if (x == 0)
                {
                    postMessage({ 'isData': false, 'x': x, 'y': y });
                }


       }    //FOR LOOP EDGE

    }        //     FOR LOOP EDGE

    //var t1 = performance.now();
    //console.log("DM for loop: " + (t1 - t0));

    distributed_array = averageArray(distributed_array, elevation_array, width, height, 10, true);

    for (var g = 0; g < height; g++ )               //uncomment here
    {
        for(var h = 0; h < width; h++)
        {
            
            /*var ny = 4*g / height;
            
            nx = Math.cos((h * 2 * Math.PI) / width); 
            nz = Math.sin((h * 2 * Math.PI) / width);

            
            var value2 = pn2.noise(nx, ny, nz) + .5 * pn2.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn2.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn2.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn2.noise(16 * nx, 16 * ny, 16*nz);

            value2 /= 1.28;
            value2 = Math.pow(value2, 2);
            */


            if(elevation_array[h][g] < sea_level)   //uncomment here
            {
                distributed_array[h][g] = 0;
            }
            //else if(averaging_array[h][g] != 0)               //If only adding el_slope and not using el_slope*m_remaining, average
            //{
            //   distributed_array[h][g] = distributed_array[h][g] / averaging_array[h][g];
            //}
            /*else
            {
                distributed_array[h][g] *= value2;


            }*/

            if(distributed_array[h][g] > maxmoist)
            {
                maxmoist = distributed_array[h][g]; 
            }
            if(distributed_array[h][g] < minmoist)
            {
                minmoist = distributed_array[h][g];
            }


       } //uncomment here
  }  //uncomment here

    //distributed_array = averageArray(distributed_array, elevation_array, width, height);

    //return distributed_array;
    postMessage({ 'isData': true, 'distArr': distributed_array, 'savedRand': Math.random.state(), 'maxMoist': maxmoist, 'minMoist':minmoist});
}