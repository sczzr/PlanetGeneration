/*
<!-- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -->
<!-- code copyright (c) 2016-2017 ProcGenesis                            -->
<!-- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -->
*/

importScripts('draw.min.js', 'perlin.js', 'seedrandom.js');

var elArray, stArray, attArray, neighArray, wid, ht;
var rockArray, oreArray, sea_level;
var savedmrandstate;


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
    stArray = data.stressArr;
    attArray = data.attributeArr;
    neighArray = data.neighborArr;
    wid = data.width;
    ht = data.height;
    rockArray = data.rockArr;
    oreArray = data.oreArr;
    sea_level = data.sl;
    savedmrandstate = data.savedmrand;

    modifyElevation_worker(elArray, stArray, attArray, neighArray, wid, ht, rockArray, oreArray);
    //console.log("ELARRAY " + elArray);
    //postMessage(attArray);

}, false);


function modifyElevation_worker(elevation_array, stress_array, attribute_array, neighbor_array, width, height, RockMap, OreMap)
{

    //console.log(elevation_array);
    //console.log(attribute_array);

    //postMessage("This is a test");
    Math.seedrandom('', { state: savedmrandstate });
    //console.log("IN FUNCTION RAND: " + Math.random());


    var modifiedElevation = matrix(width, height, 0);
    var modifiedPressure = matrix(width, height, 0);
    var edgeonlyarray = [];
    
    var distance = 0;       
    
    //var pn = new Perlin(PerlinSeeds[3]);
    var pn = new Perlin(Math.random());
    var nx, ny, nz = 0;
    var value = 0;

    var rockpn = new Perlin(Math.random());
    var rnx, rny, rnz = 0;
    var rockval = 0;

    var orepn = new Perlin(Math.random());
    var onx, ony, onz = 0;
    var oreval = 0;
    var oreval2 = 0;
    
    //var ring_count = 1;
    
    //var wrapp = 0;
    //var wrapq = 0;

    var neighborlist = [];

    var pressure_array = [];
    var edgeonlyarray = [];
    var distance_array = [];
    
    var total_pressure = 0;
    var total_distance = 0;

    //var neighbordexstart = 0;
    //var neighbordexend = 0;

    var last_id = 0;


    var disttotal = 0;

    var shortdisttracker = Infinity;
    //var count = 0;

    var distance_factor = 0;
    var dx = 0;

    var best_distance = Infinity;
    var neighborx = 0;
    var neighbory = 0;

    var dist_proportion = 0;
    var distance_total = 0;

    var modifiedbaseel = 0; 
    //go through each pixel
    
    for (var i = 0; i < width; i++ )            //creating an array of edges only. Convert later to find nearest edge by increasing radius instead
    {
        for(var j = 0; j < height; j++)
        {
            if(stress_array[i][j].isBorder == 1)
            {
                edgeonlyarray.push({ x: i, y: j, id: stress_array[i][j].pair_id.id0, neighbor_id: stress_array[i][j].pair_id.id1, type: stress_array[i][j].type, isOceanic: attribute_array[i][j].isOceanic });

            }
        }
    }

    edgeonlyarray.sort(function (a, b) { return a.id - b.id || a.neighbor_id - b.neighbor_id;});

    //console.log(edgeonlyarray);
    //var wrapcount = 0;
    //var notwrapcount = 0;

    //var gradient_init_value = Math.random() * 1.75 - .5;
    //var gradient_init_value = -.3;
    var gradient_init_value = Math.random();

    //if(sea_level <= .25)
    //{
    //    gradient_init_value = Math.random() * 1.25;
    //}
    var gradient_coefficient = Math.random() * .1 + .1;


    //console.log("G_I_V");
    //console.log(gradient_init_value);
    var progresspercent = 0;

    var lowrvalue = Infinity;
    var highrvalue = -Infinity;

    var lowovalue = Infinity;
    var highovalue = -Infinity;

    var lowovalue2 = Infinity;
    var highovalue2 = -Infinity;

    var timer;
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
           
            //if (elevation_array[x][y] * attribute_array[x][y].baseEl >= .35)
            //{
                //timer = setTimeout(elevationLoop(-1,-1), 0);
                //progresspercent += 1/(height*width);
                //setTimeout(function(){drawLoadBar(2 / 9 + progresspercent/9);}, 50);



                

                //elevationLoop(x,y);
                //function elevationLoop(x,y){
                pressure_array = [];
                distance_array = [];
                total_distance = 0;
                total_pressure = 0;
                last_id = 0;

                /*if(x == (width-1) && y == (height-1))
                {
                    stop();
                }

                x++;
                x = x % width;

                if(x % width == 0)
                {
                    y++;
                }*/



                neighborlist = neighbor_array.slice(neighbor_array.map(function (e) { return e.id; }).indexOf(stress_array[x][y].pair_id.id0), neighbor_array.map(function (e) { return e.id; }).lastIndexOf(stress_array[x][y].pair_id.id0) + 1);


                for (var j = 0; j < neighborlist.length; j++)
                {
                    distance_array.push(Infinity);
                    //console.log(distance_array);
                }
                //console.log(distance_array);
                //use with for loop going through neighborlist instead of edgeonlyarray

                for (var a = 0; a < neighborlist.length; a++)
                {
                    //console.log(a);
                    //neighbordexstart = edgeonlyarray.findIndex(x => x.id==neighborlist[a].neighbor);
                    //console.log(neighbordexstart);
                    //neighbordexstart = edgeonlyarray.map(function (e) { return e.id; }).indexOf(neighborlist[a].neighbor);          //find start and end indices for that particular neighbor pair, so you only have to go through part of edgeonlyarray
                    //neighbordexend = edgeonlyarray.map(function (e) { return e.id; }).lastIndexOf(neighborlist[a].neighbor);
                    /*if(neighborlist[a].type == "t")
                    {   
                        distance_array[a] = null;
                        pressure_array[a] = { id: neighborlist[a].id, neighbor: neighborlist[a].neighbor, neighborx: null, neighbory: null, direct_force: neighborlist[a].direct_force /*stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct*///, closest_distance: null, type: null };
                    //    continue;
                    //}

                    for(var n = 0; n < edgeonlyarray.length; n++)
                    {
                        //console.log("xy: "+x+" "+y+" a " +a+ " n "+n+" last_id" + last_id);
                        //console.log(n);
                        //console.log(edgeonlyarray[n].id + " / " +edgeonlyarray.length);
                        //console.log("NL Direct: " + neighborlist[a].direct_force + " SA Direct: " + stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct);
                        if (edgeonlyarray[n].id == neighborlist[a].neighbor) //&& attribute_array[edgeonlyarray[n].x][edgeonlyarray[a].y].baseEl * elevation_array[edgeonlyarray[n].x][edgeonlyarray[n].y] >= .35)
                        {   

                            //console.log("NL Direct: " + neighborlist[a].direct_force + " SA Direct: " + stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct);
                            //console.log("in if");

                            //NEED TO CHECK FOR WRAP WHEN CALCULATING DISTANCE - FIND DIST NORMALLY AND WITH WRAP, SEE WHICH IS SMALLER
                            //var wrapdebug = edgeonlyarray[n].x + width - x;
                            //console.log("WrapDebug " + wrapdebug);
                            //console.log(("w: " + edgeonlyarray[n].x + width - x) + "nw: " + (edgeonlyarray[n].x - x));
                            //var xwrapcheck = 0;
                            dx = Math.abs(edgeonlyarray[n].x - x);
                            if(dx > width/2){
                            
                            //if((edgeonlyarray[n].x + width - x) < (Math.abs(edgeonlyarray[n].x - x)))  //Check for Wrap
                            //{
                                xwrapcheck = width - dx;
                                //wrapcount++;
                                //console.log("Wrap"); 
                                 
                            }
                            else
                            {
                                xwrapcheck = dx;
                                //notwrapcount++;
                                //console.log("No wrap");
                            }
                            
                                               
                            distance = Math.sqrt(((xwrapcheck) * (xwrapcheck)) + ((edgeonlyarray[n].y - y) * (edgeonlyarray[n].y - y)));

                            if (distance < distance_array[a])
                            {
                                distance_array[a] = distance;
                                pressure_array[a] = { id: neighborlist[a].id, neighbor: neighborlist[a].neighbor, neighborx: edgeonlyarray[n].x, neighbory: edgeonlyarray[n].y, direct_force: neighborlist[a].direct_force /*stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct*/, shear_force: neighborlist[a].shear_force, closest_distance: distance, type: edgeonlyarray[n].type };

                            }

                            //var neighbornum = neighborlist[a].neighbor;

                            //if(last_id != neighbornum)
                            //{
                            //    break;
                            //}
                            
                        }
                        if(edgeonlyarray[n].id == (neighborlist[a].neighbor + 1))
                        {
                            break;
                        }

                        
                            //last_id = neighbornum;
                        

                    }

                }
                //for (var a = 0; a < edgeonlyarray.length; a++)
                //{

                    //if (a % 100 == 0)
                    //{
                    //console.log(edgeonlyarray[a]);
                    //}
                    /*neighborlist.some(function (entry, n)
                    {
                    if (entry.neighbor == edgeonlyarray[a].id)
                    {
                    neighbordex = n;
                    return true;
                    }
                    });*/

                    /*
                    neighbordex = neighborlist.map(function (e) { return e.neighbor; }).indexOf(edgeonlyarray[a].id);
                    if(neighbordex > -1)
                    {
                    var distance = Math.sqrt((edgeonlyarray[a].x - x)*(edgeonlyarray[a].x - x) + (edgeonlyarray[a].y - y)*(edgeonlyarray[a].y - y));

                    if (distance < distance_array[neighbordex])
                    {
                    distance_array[neighbordex] = distance;
                    pressure_array[neighbordex] = { direct_force: neighborlist[neighbordex].direct_force, closest_distance: distance };


                    }
                    
                    //pressure_array.push({direct_force: neighborlist[neighbordex].direct_force, closest_distance: distance});
                    //neighborlist.splice(neighbordex, 1);
                    //total_distance += distance;

                    }*/

                    //if(neighborlist.length < 1)
                    //{
                    //console.log("for broken");
                    //  break;
                    //}

                    //count++;

                //}



                //---------------------OLD CODE---------------
                /*for(ring_count = 1; ring_count < 25; ring_count++)
                {

                //for each pixel, search for nearest neighbor edges
                for(var p = (-1*ring_count); p <= ring_count; p++)
                {
                for(var q = (-1*ring_count); q <= ring_count; q++)
                {
                wrapp = x + p;
                wrapq = y + q;


                if (wrapp < 0)
                {
                //wrapxl = width - x;
                wrapp = width + wrapp;
                }
                if (wrapp >= width)
                {
                wrapp = wrapp % width;
                //wrapxr = x % width - 1;
                }
                if (wrapq < 0)                   //y doesn't wrap, so don't sample values on other side of image
                {
                continue;
                //wrapq = 0;
                //wrapyu = height - y;
                }
                if (wrapq >= height)
                {
                continue;
                //wrapq = height - 1;
                //wrapyb = y % height - 1;
                }

                if (Math.abs(p) == ring_count || Math.abs(q) == ring_count) //make sure the points are only in the "ring" not in the filled circle
                {
                var neighbordex = neighborlist.map(function (e) { return e.neighbor; }).indexOf(stress_array[wrapp][wrapq].pair_id.id0);
                if(neighbordex > -1)
                {
                var distance = Math.sqrt((wrapp - x)*(wrapp - x) + (wrapq - y)*(wrapq - y));
                                      
                pressure_array.push({direct_force: neighborlist[neighbordex].direct_force, closest_distance: distance});
                neighborlist.splice(neighbordex, 1);
                total_distance += distance;

                }



                }

                }
                }   
                if(neighborlist.length < 1)
                {
                break;
                }
                }*/ // -----------------------END OLD CODE---------------


                //console.log(distance_array);

                //function getSum(total, num) { return total + num;}
                //total_distance = distance_array.reduce(getSum, 0);
                best_distance = Infinity;
                
                //--------------rock variables-------------------
                var best_dpressure = 0;
                var best_spressure = 0;
                var best_type = "";

                var total_shear = 0;
                //-----------------------------------------------
                

                //neighborx = 0;
                //neighbory = 0;
                //console.log("entering loop");
                distance_total = 0;
                
                for (var b = 0; b < pressure_array.length; b++)
                {
                
                        //if(pressure_array[b].closest_distance == null)
                        //{
                        //    continue;
                        //}


                        //if (x < 600 && y < 300)
                        //{
                        //    console.log("(x,y): (" + x + ", " + y + ") ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor + " Distance: " + pressure_array[b].closest_distance + " BestDistance: " + best_distance);
                        //}
                    //console.log("ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor);
                    if(pressure_array[b].closest_distance < best_distance)
                    {   

                        //if (x < 600 && y < 300)
                        //{
                        //    console.log("(x,y): (" + x + ", " + y + ") ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor + " Distance: " + pressure_array[b].closest_distance + " BestDistance: " + best_distance);
                        //}
                        //console.log("(x,y): ("+x+", "+y+") ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor + " Distance: "+pressure_array[b].closest_distance+" BestDistance: "+best_distance);
                        best_distance = pressure_array[b].closest_distance;
                        //neighborx = pressure_array[b].neighborx;
                        //neighbory = pressure_array[b].neighbory;
                        //distance_factor = .4 / (.005 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                        //total_pressure = pressure_array[b].direct_force; // * distance_factor;

                        best_dpressure = pressure_array[b].direct_force;
                        best_spressure = pressure_array[b].shear_force;
                    
                    }
                   // dist_proportion = 1 - (pressure_array[b].closest_distance / total_distance);
                
                    //console.log("dist prop: " + dist_proportion);
                
                
                
                    //distance_factor = .4 / (.005 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                    //balance_factor = 1.2 / (1 + (Math.pow(100, (-1*.3 * pressure_array[b].direct_force)))) - .6;


                    if (pressure_array[b].type != "t")
                    {   
                        distance_factor = .4 / (.02 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                        //total_pressure += (pressure_array[b].direct_force * distance_factor);
                        //total_pressure += distance_factor; //balance_factor; //* distance_factor;
                    }
                    else
                    {   
                        distance_factor = .2 / (.002 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                        //total_pressure += pressure_array[b].direct_force*distance_factor;
                    }

                    total_pressure += pressure_array[b].direct_force*distance_factor;
                    distance_total += distance_factor;// * pressure_array[b].direct_force;
                    //total_pressure += (pressure_array[b].direct_force * dist_proportion);
                    //console.log("bd in loop: "+best_distance);
                    //disttotal += (best_distance/25);

                    total_shear += pressure_array[b].shear_force*distance_factor;

                    if(best_distance == pressure_array[b].closest_distance)
                    {
                       best_type = pressure_array[b].type;   
                    }



                }

                //-----------------Rock calculations-------------------------
                var igneous_chance = 0;
                var metamorphic_chance = 0;

                rny = 4*y / height;
                        
                rnx = Math.cos((x * 2 * Math.PI) / width); 
                rnz = Math.sin((x * 2 * Math.PI) / width);

                        //Create Elevation Noise
                        //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
                        //var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                rockvalue = rockpn.noise(rnx, rny, rnz) + .5 * rockpn.noise(2*rnx, 2*rny, 2*rnz) + .25 * rockpn.noise(4*rnx, 4*rny, 4*rnz) + .125 * rockpn.noise(8 * nx, 8 * ny, 8 * nz);
                rockvalue *= .7;
                rockvalue = Math.pow(rockvalue, 2);
                //rockvalue *= 1 / (1 + (Math.pow(100, (-1 * 5 * (value - .8)))));


                if (best_type == "c")
                {
                    igneous_chance = 1 / (.00001 * best_distance * best_distance * best_distance + 1);
                }
                else
                {
                    igneous_chance = 1 / (.00005 * best_distance * best_distance * best_distance + 1);
                }

                igneous_chance += rockvalue * 4 - 2;
                //console.log("igneous chance " + igneous_chance);

                //console.log("total press + total shear / 2");
                //console.log(((Math.abs(total_pressure) + Math.abs(total_shear)) / 2) + (rockvalue*1 -.5));
                //console.log("rvalue " + rockvalue);
                if(best_type == "t")
                {
                    //metamorphic_chance = .
                    if(((Math.abs(total_pressure) + Math.abs(total_shear))/2) + (rockvalue*2 -1) >= .09)
                    {

                        //if (Math.random() < .6)
                        //{

                            RockMap[x][y] = "metamorphic";
                        //}
                    }


                }
                else
                {
                    if(((Math.abs(total_pressure) + Math.abs(total_shear))/2) + (rockvalue*2 - 1) >= .1)
                    {
                        //if (Math.random() < .5)
                        //{
                            RockMap[x][y] = "metamorphic";
                        //}
                    }
                }


                if (RockMap[x][y] != "metamorphic")
                {


                    if(rockvalue > .4 || igneous_chance > .25)
                    {
                        RockMap[x][y] = "igneous";
                    }

                    /*if(igneous_chance > .25)
                    {

                        RockMap[x][y] = "igneous";
                    }*/

                    //if (Math.random() < igneous_chance)
                    //{
                    //    RockMap[x][y] = "igneous";
                    //}
                    else
                    {
                        RockMap[x][y] = "sedimentary";
                    }
                }

                if(rockvalue < lowrvalue)
                {
                    lowrvalue = rockvalue;
                }
                if(rockvalue > highrvalue)
                {
                    highrvalue = rockvalue;
                }

                //-------------------------------------------------------------

                //------------------Ore Calc-----------------------------------


                ony = 4*y / height;
                        
                onx = Math.cos((x * 2 * Math.PI) / width); 
                onz = Math.sin((x * 2 * Math.PI) / width);

                orevalue = /*orepn.noise(onx, ony, onz) + .5 * orepn.noise(2*onx, 2*ony, 2*onz) + */.25 * orepn.noise(4*onx, 4*ony, 4*onz) + .125 * orepn.noise(8 * onx, 8 * ony, 8 * onz) + .0625 * orepn.noise(16*onx, 16*ony, 16*onz);
                //orevalue = orepn.noise(onx, ony, onz) + .5 * orepn.noise(2*onx, 2*ony, 2*onz) + .25 * orepn.noise(4*onx, 4*ony, 4*onz) + .125 * orepn.noise(8 * onx, 8 * ony, 8 * onz) + .0625 * orepn.noise(16*onx, 16*ony, 16*onz);
                //orevalue *= .7;
                //orevalue = Math.pow(orevalue, 2);

                //orevalue += (Math.random() * .3 - .15);
                //orevalue += (Math.random() * .1 - .05);
                //orevalue += (Math.random() * .04 - .02);
                //orevalue = (orevalue + rockvalue) / 2;
                //orevalue +=  rockvalue*.3 - .15;
                //orevalue = (orevalue + (rockvalue*.7 - .35)) / 2;
                //orevalue = (orevalue * (rockvalue*.7 - .35));
                orevalue = ((orevalue - .07) / .3);
                orevalue2 = (orevalue + rockvalue) / 2;


                if(orevalue < .325 || orevalue > .675)
                {
                    if(RockMap[x][y] == "sedimentary")
                    {
                    
                        if(orevalue2 < .28)
                        {
                            OreMap[x][y] = "coal";
                        }
                        else if(orevalue2 < .35 && orevalue2 >= .28)
                        {
                            OreMap[x][y] = "copper";
                        }    
                        else if(orevalue2 < .45 && orevalue2 >= .35)
                        {
                            OreMap[x][y] = "tin";
                        }
                        else if(orevalue2 < .5 && orevalue2 >= .45)
                        {
                            OreMap[x][y] = "iron";
                        }
                        else if(orevalue2 < .58 && orevalue2 >= .5)
                        {
                            OreMap[x][y] = "gold";
                        }
                        else if(orevalue2 >= .58)//< .7)
                        {
                            OreMap[x][y] = "diamond";
                        }
                        else
                        {
                            OreMap[x][y] = "none";
                        }
                    }        
                    else if(RockMap[x][y] == "igneous")
                    {

                        if(orevalue2 < .22)
                        {
                            OreMap[x][y] = "copper";
                        }
                        else if(orevalue2 < .28 && orevalue2 >= .22)
                        {
                            OreMap[x][y] = "platinum";
                        }    
                        else if(orevalue2 < .37 && orevalue2 >= .28)
                        {
                            OreMap[x][y] = "aluminum";
                        }
                        else if(orevalue2 < .46 && orevalue2 >= .37)
                        {
                            OreMap[x][y] = "iron";
                        }
                        else if(orevalue2 < .55 && orevalue2 >= .46)
                        {
                            OreMap[x][y] = "silver";
                        }
                        else if(orevalue2 < .62 && orevalue2 >= .55)
                        {
                            OreMap[x][y] = "tin";
                        }
                        else if(orevalue2 >= .62)//< .7)
                        {
                            OreMap[x][y] = "diamond";
                        }
                        else
                        {
                            OreMap[x][y] = "none";
                        }
                    }    
                    else
                    {
                        if(orevalue2 < .28)
                        {
                            OreMap[x][y] = "copper";
                        }
                        else if(orevalue2 < .36 && orevalue2 >= .28)
                        {
                            OreMap[x][y] = "lead";
                        }    
                        else if(orevalue2 < .51 && orevalue2 >= .36)
                        {
                            OreMap[x][y] = "silver";
                        }
                        else if(orevalue2 < .62 && orevalue2 >= .51)
                        {
                            OreMap[x][y] = "gold";
                        }
                        else if(orevalue2 >= .67)
                        {
                            OreMap[x][y] = "diamond";
                        }
                        else
                        {
                            OreMap[x][y] = "none";
                        }
                    }




                }
                else
                {
                  OreMap[x][y] = "none";  
                }



                /*if(orevalue < .33)
                {
                    OreMap[x][y] = "copper";
                }
                else if(orevalue >= .33 && orevalue < .66)
                {
                    OreMap[x][y] = "coal";
                }
                else if(orevalue >= .66)
                {
                    OreMap[x][y] = "gold";   
                }*/
                /*if(RockMap[x][y] == "sedimentary")
                {
                    console.log("Sedimentary");
                    console.log("Orevalue: " + orevalue);
                    

                    //if(orevalue < .3)
                    //{
                    //    OreMap[x][y] = "none";
                    //}
                    if(orevalue < .2)
                    {
                        OreMap[x][y] = "copper";
                    }
                    else if(orevalue < .35 && orevalue > .32)
                    {
                        OreMap[x][y] = "diamond";
                    }
                    else if(orevalue < .43 && orevalue > .39)
                    {
                        OreMap[x][y] = "iron";
                    }
                    else if(orevalue < .5 && orevalue > .45)
                    {
                        OreMap[x][y] = "tin";
                    }
                    else if(orevalue < .585 && orevalue > .55)
                    {
                        OreMap[x][y] = "gold";
                    }
                    else if(orevalue < .67 && orevalue > .6)
                    {
                        OreMap[x][y] = "coal";
                    }
                    else
                    {
                        OreMap[x][y] = "none";
                    }
                    
                        
                }
                else if(RockMap[x][y] == "igneous")
                {
                    console.log("Igneous");
                    console.log("Orevalue: " + orevalue);
                    

                    //if(orevalue < .3 && orevalue > .25)
                    //{
                    //    OreMap[x][y] = "none";
                    //}
                    if(orevalue < .25 && orevalue > .2)
                    {
                        OreMap[x][y] = "copper";
                    }
                    else if(orevalue < .27 && orevalue > .24)
                    {
                        OreMap[x][y] = "diamond";
                    }
                    else if(orevalue < .35 && orevalue > .3)
                    {
                        OreMap[x][y] = "iron";
                    }
                    else if(orevalue < .42 && orevalue > .37)
                    {
                        OreMap[x][y] = "tin";
                    }
                    else if(orevalue < .49 && orevalue > .53)
                    {
                        OreMap[x][y] = "silver";
                    }
                    else if(orevalue < .58 && orevalue > .55)
                    {
                        OreMap[x][y] = "platinum";
                    }
                    else if(orevalue < .65 && orevalue > .6)
                    {
                        OreMap[x][y] = "aluminum";
                    }
                    else
                    {
                        OreMap[x][y] = "none";
                    }
                }
                else
                {
                    console.log("Metamorphic");
                    console.log("Orevalue: " + orevalue);
                    
                    
                    //if(orevalue < .3)
                    //{
                    //    OreMap[x][y] = "none";
                    //}
                    if(orevalue < .25 && orevalue > .2)
                    {
                        OreMap[x][y] = "copper";
                    }
                    else if(orevalue < .32 && orevalue > .27)
                    {
                        OreMap[x][y] = "lead";
                    }
                    else if(orevalue < .39 && orevalue > .35)
                    {
                        OreMap[x][y] = "silver";
                    }
                    else if(orevalue < .455 && orevalue > .42)
                    {
                        OreMap[x][y] = "gold";
                    }
                    else
                    {
                        OreMap[x][y] = "none";
                    }
                    
                        
                }*/

                //console.log(OreMap[x][y]);



                //orevalue = ((orevalue - .05) / .3);
                //OreMap[x][y] = orevalue;
                


                if(orevalue < lowovalue)
                {
                    lowovalue = orevalue;
                }
                if(orevalue > highovalue)
                {
                    highovalue = orevalue;
                }

                 if(orevalue2 < lowovalue2)
                {
                    lowovalue2 = orevalue2;
                }
                if(orevalue2 > highovalue2)
                {
                    highovalue2 = orevalue2;
                }






                //total_pressure /= pressure_array.length;
                //console.log("bd after loop: "+best_distance);
                //best_distance /= 25; //possibly modify best distance???-----------

                //if(best_distance < shortdisttracker)
                //{
                //    shortdisttracker = best_distance;
                //}

                //modifiedPressure[x][y] = total_pressure;
                //console.log("TP " + total_pressure);

                modifiedbaseel = attribute_array[x][y].baseEl + ((1 / (.01 * (best_distance*best_distance) + 1)) * (1 - attribute_array[x][y].baseEl));

                //console.log(total_pressure);
                //modifiedElevation[x][y] += (total_pressure + elevation_array[x][y]);
                

                //modifiedElevation[x][y] = .5 + total_pressure;
                //modifiedElevation[x][y] = distance_total;
                //console.log("DT " + distance_total);



                ny = 4*y / height;
                        
                nx = Math.cos((x * 2 * Math.PI) / width); 
                nz = Math.sin((x * 2 * Math.PI) / width);

                        //Create Elevation Noise
                        //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
                        //var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                value = .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz) + .03125 * pn.noise(32 * nx, 32 * ny, 32 * nz);
                value *= 7;
                value *= 1 / (1 + (Math.pow(100, (-1 * 5 * (value - .8)))));

                //var gradient_value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                //gradient_value /= 1.28;
                //gradient_value = Math.pow(gradient_value, 2);
                           
                //value *= 1 / (1 + (Math.pow(100, (-1*5 * (value - .1)))));
                //total_pressure *= value;
                //var gradient_init_value = Math.random() * 1.75 - .5;
                var gradient_factor = (y / (gradient_coefficient /*.1*/ * height)) + gradient_init_value; //(y / (height / 10));
                
                if(y >= (height * ( gradient_coefficient /*.1*/ * gradient_init_value + (1 - gradient_coefficient)/*.9*/)))
                {
                    gradient_factor = -1 * (1 / (gradient_coefficient /*.1*/ * height)) * (y - ((1 - gradient_coefficient) /*.9*/ * height)) + 1 + gradient_init_value;
                    //if (x == 0)
                    //{
                    //    console.log("y: " + y + " gf: " + gradient_factor);
                    //}
                    //gradient_factor *= gradient_value;
                }
                //if (y <= .2 * height)
                //{
                    //gradient_factor *= gradient_value;
                //}
                
                //if(y <= .15* height || y >= .85*height)
                //{
                //    gradient_factor *= gradient_value;
                //}
                if(gradient_factor > 1)
                {
                    gradient_factor = 1;
                }


                //drawArray(x, y, gradient_factor, "myCanvas12");

                if(((elevation_array[x][y] + (total_pressure*.7*value)) * (attribute_array[x][y].baseEl) /** gradient_factor*/) >= sea_level)//if (elevation_array[x][y] * attribute_array[x][y].baseEl >= .35) //&& attribute_array[neighborx][neighbory].baseEl * elevation_array[neighborx][neighbory] >= .35) //uncomment here
                {
                    //modifiedElevation[x][y] = elevation_array[x][y];
                    //modifiedElevation[x][y] = total_pressure;
                    modifiedElevation[x][y] += ((elevation_array[x][y]+(total_pressure*value)) * modifiedbaseel * gradient_factor);
                    modifiedElevation[x][y] = modifiedElevation[x][y] + (.15 * (1 - modifiedElevation[x][y])) - .12;
                }
                else
                {
                    modifiedElevation[x][y] += ((elevation_array[x][y] + (total_pressure*.7*value)) * (attribute_array[x][y].baseEl*modifiedbaseel));
                    //modifiedElevation[x][y] = ((elevation_array[x][y] + total_pressure) * .3*modifiedbaseel);//attribute_array[x][y].baseEl);
                    //modifiedElevation[x][y] = modifiedElevation[x][y] + (.15 * (1 - modifiedElevation[x][y]));
                }
               
               
                //console.log(total_pressure);
                //modifiedElevation[x][y] *= 2*total_pressure; //try modifying formula for total pressure (get more influence from close by borders
           // }
              //if(stress_array[x][y].isBorder == 1 && stress_array[x][y].direct != stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].direct){
              //  console.log("SA Direct: "+stress_array[x][y].direct+" SA Neighbor Direct: "+stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].direct + " Current X/Y: "+x+", "+y+ " Neighbor X/Y: "+stress_array[x][y].neighbor.x+", "+stress_array[x][y].neighbor.y+" Neighbor's Neighbor: "+stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].neighbor.x+", "+stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].neighbor.y);
              //}

                //progresspercent += 1/(height*width);
                //drawLoadBar(2 / 9 + (progresspercent/12));


                /*if(x == (width-1) && y == (height-1))
                {
                    stop();
                }

                x++;
                x = x % width;

                if(x % width == 0)
                {
                    y++;
                }*/

                //timer = setTimeout(elevationLoop(x, y), 0);


              //}




              //function stop()
              //{
              //    clearInterval(timer);
              //}
                //progresspercent += 1/(height*width);
                //drawLoadBar(2 / 9 + (progresspercent/12));

                if (x == 0)
                {
                    postMessage({ 'isData': false, 'x': x, 'y': y });
                }
               
        }
    }
    //console.log("LOW R VALUE " + lowrvalue);
    //console.log("High R VALUE " + highrvalue);
    //console.log("LOW O VALUE " + lowovalue);
    //console.log("High O VALUE " + highovalue);
    //console.log("LOW O2 VALUE " + lowovalue2);
    //console.log("High O2 VALUE " + highovalue2);
    //console.log("WrapCount: "+wrapcount);
    //console.log("NotWrapCount: " + notwrapcount);
    //console.log("WrapDebug " + wrapdebug);
    //TEST
    //for (var k = 0; k < edgeonlyarray.length; k++ )
    //{

    //    modifiedElevation[edgeonlyarray[k].x][edgeonlyarray[k].y] = Math.abs(stress_array[edgeonlyarray[k].x][edgeonlyarray[k].y].direct); 

    //}





        //disttotal /= (width * height);
    //console.log(disttotal);
    //console.log("Shortest Dist: "+shortdisttracker);
    //console.log(count);

   // modifiedElevation = averageArray(modifiedElevation, elevation_array, width, height, 3, false);
    //modifiedPressure = averageArray(modifiedPressure, elevation_array, width, height);

    //for (y = 0; y < height; y++)
    //{
    //    for (x = 0; x < width; x++)
    //    {
    //        modifiedElevation[x][y] = (elevation_array[x][y] + modifiedPressure[x][y]) * modifiedElevation[x][y];
    //    }
   // }
    isElevationComplete = true;
    
    
    //return modifiedElevation;
    //console.log("LAST IN FUNCTION RAND " + Math.random());
    postMessage({ 'isData': true, 'elArr': modifiedElevation, 'rockArr': RockMap, 'oreArr': OreMap, 'savedRand': Math.random.state()});

    
    //return modifiedPressure;    

}



//modifyElevation_worker(elArray, stArray, attArray, neighArray, wid, ht, rockArray, oreArray);