 private void CopyProps<T>(T fromObj, T toObj, string[] propsToIgnore)
{
	var fromObjProps = fromObj.GetType().GetProperties();
	foreach (var prop in fromObjProps)
	{
		if (propsToIgnore != null && propsToIgnore.Contains(prop.Name))
			continue;

		toObj.GetType().GetProperty(prop.Name).SetValue(toObj, prop.GetValue(fromObj, null), null);
	}
}

private T CloneObject<T>(T obj, string[] propsToIgnore)
{
	T newObj = (T)Activator.CreateInstance(typeof(T));

	var props = obj.GetType().GetProperties();
	foreach (var prop in props)
	{
		if (propsToIgnore != null && propsToIgnore.Contains(prop.Name))
			continue;

		newObj.GetType().GetProperty(prop.Name).SetValue(newObj, prop.GetValue(obj, null), null);
	}

	return newObj;
}