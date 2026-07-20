record OpenWeatherForecastResponse(List<OpenWeatherForecastItem> List, OpenWeatherCity City);

record OpenWeatherForecastItem(long Dt, string DtTxt, OpenWeatherMain Main, List<OpenWeatherCondition> Weather);

record OpenWeatherMain(double Temp);

record OpenWeatherCondition(string Description);

record OpenWeatherCity(string Name, string Country);
